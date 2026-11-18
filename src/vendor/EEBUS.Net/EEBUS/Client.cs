using EEBUS.Enums;
using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.SHIP.Messages;
using EEBUS.SPINE.Commands;
using Makaretu.Dns;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;

namespace EEBUS
{
	public class Client : Connection
	{
		public Client(HostString host, WebSocket ws, Devices devices, RemoteDevice remoteDevice, ILogger? logger = null, ILogger? spineLogger = null)
			: base(host, ws, devices, logger, spineLogger)
		{
			this.Remote = remoteDevice;
		}

		private Task? _runTask;
		private readonly Lock _runLock = new();

		public Task Run(CancellationToken cancellationToken = default)
		{
			Logger?.LogDebug("Running new Client for device " + this.Remote?.Name);

			lock (_runLock)
			{
				if (_runTask != null)
				{
					// Already started; return the existing task so callers awaiting
					// a second Run() observe the same lifetime instead of starting
					// a parallel receive loop on the same socket.
					return Task.CompletedTask;
				}

				_runTask = Task.Run(async () =>
				{
					try
					{
						await RunInternalAsync(cancellationToken).ConfigureAwait(false);
					}
					catch (Exception ex)
					{
                        Logger?.LogError(ex, "Client RunInternal escaped exception.");
						// Observe any exception that escaped RunInternalAsync so it
						// doesn't become an UnobservedTaskException.
					}
				});
			}

			// Return immediately; callers (EEBUSManager.ConnectAsync, reconnect loop)
			// are not expected to block for the lifetime of the connection.
			return Task.CompletedTask;
		}

		/// <summary>
		/// Awaits the background receive loop, if one was started. Useful for
		/// orderly shutdown paths that want to ensure the worker actually exited.
		/// </summary>
		public Task WaitForCompletionAsync()
		{
			lock (_runLock)
			{
				return _runTask ?? Task.CompletedTask;
			}
		}

		public override async Task CloseAsync()
		{
			// Best-effort: send a clean WebSocket close. The receive loop will then
			// see the closure, exit, and the finally block in RunInternalAsync will
			// run timer disposal etc.
			try
			{
				if (this.ws.State == WebSocketState.Open || this.ws.State == WebSocketState.CloseReceived)
				{
					await this.ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing!", CancellationToken.None).ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
               Logger?.LogError(ex, "Client.CloseAsync: CloseOutputAsync failed.");
			}

			// Dispose the underlying socket. The Client took ownership of it from
			// EEBUSManager.ConnectAsync (see the "Ownership transferred" comment
			// there); nothing else releases it.
			try
			{
				this.ws.Dispose();
			}
			catch (Exception ex)
			{
              Logger?.LogError(ex, "Client.CloseAsync: WebSocket dispose failed.");
			}
		}

		private async Task RunInternalAsync(CancellationToken cancellationToken)
		{
			this.state = EState.Disconnected;
			this.subState = ESubState.None;

			InitMessage initMessage = new InitMessage();

			await initMessage.Send(this.ws, Logger).ConfigureAwait(false);

			var heart = new HeartBeatTask();
			// We declare heartbeatTimeout="PT4S" (DeviceDiagnosisHeartbeatData.cs) - per SPINE TS
			// Resource Specification, that's the period our own heartbeat notifies are expected in,
			// not a remaining-time countdown the peer resets, but a peer strictly enforcing "no
			// heartbeat within 4s = dead connection" leaves zero margin if our send timer also fires
			// at exactly 4000ms - any scheduling jitter (GC, queueing) pushes us past it. Send well
			// inside that window instead. Observed live: with 4000ms, this wallbox closed and
			// reconnected on a tight ~4s cycle repeatedly; not yet re-verified with this margin.
			Timer beat = new System.Threading.Timer(arg => heart.Beat(arg), this, 1500, 1500);
            //var ecc = new ElectricalConnectionCharacteristicTask();
            //var eccSend = new System.Threading.Timer(ecc.SendData, this, 2000, Timeout.Infinite);

            //var md = new MeasurementDataTask();
            //var mdSend = new System.Threading.Timer(md.SendData, this, 3000, 3000);

            if (this.Remote != null)
            {
                this.Remote.LastDisconnectUtc = null;
            }

            try
			{
				while (this.state != EState.Stopped && !cancellationToken.IsCancellationRequested)
				{

					using CancellationTokenSource timeoutCts = new CancellationTokenSource(SHIPMessageTimeout.CMI_TIMEOUT);
					using CancellationTokenSource linkedTokenSource =
						CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

					var message = await ReceiveAsync(linkedTokenSource.Token);

					(this.state, this.subState, string error) = message.ClientTest(this.state);

					if (this.state == EState.Stopped && error != null)
						throw new Exception(error);
					if (error != null)
                        Logger?.LogError(error);

					EState oldState = this.state;
					(this.state, this.subState) = await message.NextClientState(this, Logger).ConfigureAwait(false);

					if (null != this.Remote)
						this.Remote.SetClientState(this.state);

					if (this.state == EState.Connected && this.state != oldState && !cancellationToken.IsCancellationRequested)
					{
						RequestRemoteDeviceConfiguration();
						// ReadAndSubscribe();
					}

					ResolvePendingRequest(message);
				}
			}
			catch (OperationCanceledException)
			{
				// Connection.ReceiveAsync throws this deliberately both when the remote sent a normal
				// WebSocket close frame and when our own shutdown cancellation token fired - neither
				// is an error, just the ordinary way this loop ends. Log it quietly instead of at
				// Error, which used to fire on every clean disconnect (including our own exit/dispose).
				Logger?.LogDebug("Client connection closed (remote closed or cancelled).");
				this.state = EState.ErrorOrTimeout;
				if (this.Remote != null)
				{
					this.Remote.LastDisconnectUtc = DateTime.UtcNow;
				}
			}
			catch (Exception ex)
			{
              Logger?.LogError(ex, "Client connection closed with error.");
				this.state = EState.ErrorOrTimeout;
				if (this.Remote != null)
				{
					this.Remote.LastDisconnectUtc = DateTime.UtcNow;
				}
			}
			finally
			{
				// Dispose the timer (Change(Infinite, Infinite) only stops further
				// callbacks; the underlying TimerQueueTimer is only released by Dispose).
				try { beat.Dispose(); } catch { }

				if (null != this.Remote)
					this.Remote.SetClientState(EState.Stopped);

				// Make sure the socket is released even if no one calls CloseAsync.
				try
				{
					await CloseAsync().ConfigureAwait(false);
				}
				catch (Exception ex)
				{
                    Logger?.LogError(ex, "Client.RunInternalAsync: CloseAsync in finally failed.");
				}
			}
		}
	}
}
