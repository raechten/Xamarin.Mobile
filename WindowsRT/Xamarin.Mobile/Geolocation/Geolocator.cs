﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.Foundation;

namespace Xamarin.Geolocation
{
    public class Geolocator
    {
		public Geolocator()
		{
			this.locator.PositionChanged += OnLocatorPositionChanged;
			this.locator.StatusChanged += OnLocatorStatusChanged;
		}

		public event EventHandler<PositionEventArgs> PositionChanged;
		public event EventHandler<PositionErrorEventArgs> PositionError;

		public bool IsGeolocationAvailable
		{
			get { return this.locator.LocationStatus != PositionStatus.NotAvailable; }
		}

		public bool IsGeolocationEnabled
		{
			get { return this.locator.LocationStatus != PositionStatus.Disabled && this.locator.LocationStatus != PositionStatus.NotAvailable; }
		}

		public double DesiredAccuracy
		{
			get { return this.desiredAccuracy; }
			set
			{
				this.desiredAccuracy = value;
				this.locator.DesiredAccuracy = (value < 100) ? PositionAccuracy.High : PositionAccuracy.Default;
			}
		}

		public bool IsListening
		{
			get { return this.isListening; }
		}

		public Task<Position> GetPositionAsync (int timeout)
		{
			if (timeout < 0)
				throw new ArgumentOutOfRangeException ("timeout");

			try
			{
				// The built in timeout does not cancel, it throws an exception, so we'll setup our own.
				IAsyncOperation<Geoposition> pos = this.locator.GetGeopositionAsync (TimeSpan.Zero, TimeSpan.FromDays (365));
				Timeout timer = new Timeout (timeout, pos.Cancel);

				var tcs = new TaskCompletionSource<Position>();

				pos.Completed = (op, s) =>
				{
					timer.Cancel();

					switch (s)
					{
						case AsyncStatus.Canceled:
							tcs.SetCanceled();
							break;
						case AsyncStatus.Completed:
							tcs.SetResult (GetPosition (op.GetResults()));
							break;
						case AsyncStatus.Error:
							tcs.SetException (op.ErrorCode);
							break;
					}
				};

				return tcs.Task;
			}
			catch (UnauthorizedAccessException)
			{
				throw new GeolocationException (GeolocationError.Unauthorized);
			}
		}

		public async Task<Position> GetPositionAsync (CancellationToken token)
		{
			try
			{
				IAsyncOperation<Geoposition> op = this.locator.GetGeopositionAsync();
				token.Register (o => ((IAsyncOperation<Geoposition>)o).Cancel(), op);

				Geoposition pos = await op.AsTask (false);
				return GetPosition (pos);
			}
			catch (UnauthorizedAccessException)
			{
				throw new GeolocationException (GeolocationError.Unauthorized);
			}
		}

		public Task<Position> GetPositionAsync (int timeout, CancellationToken token)
		{
			if (timeout < 0)
				throw new ArgumentOutOfRangeException ("timeout");

			try
			{
				IAsyncOperation<Geoposition> pos = this.locator.GetGeopositionAsync (TimeSpan.FromTicks (0), TimeSpan.FromDays (365));
				token.Register (o => ((IAsyncOperation<Geoposition>)o).Cancel(), pos);

				Timeout timer = new Timeout (timeout, pos.Cancel);

				var tcs = new TaskCompletionSource<Position>();

				pos.Completed = (op, s) =>
				{
					timer.Cancel();

					switch (s)
					{
						case AsyncStatus.Canceled:
							tcs.SetCanceled();
							break;
						case AsyncStatus.Completed:
							tcs.SetResult (GetPosition (op.GetResults()));
							break;
						case AsyncStatus.Error:
							tcs.SetException (op.ErrorCode);
							break;
					}
				};

				return tcs.Task;
			}
			catch (UnauthorizedAccessException)
			{
				throw new GeolocationException (GeolocationError.Unauthorized);
			}
		}

		public void StartListening (int minTime, double minDistance)
		{
			if (minTime < 0)
				throw new ArgumentOutOfRangeException ("minTime");
			if (minTime < minDistance)
				throw new ArgumentOutOfRangeException ("minDistance");
			if (this.isListening)
				throw new InvalidOperationException();

			this.isListening = true;

			this.locator.ReportInterval = (uint)minTime;
			this.locator.MovementThreshold = minDistance;
		}

		public void StopListening()
		{
			if (!this.isListening)
				return;

			this.isListening = false;
		}

	    private bool isListening;
		private double desiredAccuracy;
		private readonly Windows.Devices.Geolocation.Geolocator locator = new Windows.Devices.Geolocation.Geolocator();

		private void OnLocatorStatusChanged (Windows.Devices.Geolocation.Geolocator sender, StatusChangedEventArgs e)
		{
			GeolocationError error;
			switch (e.Status)
			{
				case PositionStatus.Disabled:
					error = GeolocationError.Unauthorized;
					break;

				case PositionStatus.NoData:
					error = GeolocationError.PositionUnavailable;
					break;

				default:
					return;
			}

			StopListening();

			OnPositionError (new PositionErrorEventArgs (error));
		}

		private void OnLocatorPositionChanged (Windows.Devices.Geolocation.Geolocator sender, PositionChangedEventArgs e)
		{
			OnPositionChanged (new PositionEventArgs (GetPosition (e.Position)));
		}

		private void OnPositionChanged (PositionEventArgs e)
		{
			var handler = this.PositionChanged;
			if (handler != null)
				handler (this, e);
		}

		private void OnPositionError (PositionErrorEventArgs e)
		{
			var handler = this.PositionError;
			if (handler != null)
				handler (this, e);
		}

		private static Position GetPosition (Geoposition position)
		{
			var pos = new Position
			{
				Accuracy = position.Coordinate.Accuracy,
				Latitude = position.Coordinate.Latitude,
				Longitude = position.Coordinate.Longitude,
				Timestamp = position.Coordinate.Timestamp,
			};

			if (position.Coordinate.Heading != null)
				pos.Heading = position.Coordinate.Heading.Value;

			if (position.Coordinate.Speed != null)
				pos.Speed = position.Coordinate.Speed.Value;

			if (position.Coordinate.AltitudeAccuracy != null)
				pos.AltitudeAccuracy = position.Coordinate.AltitudeAccuracy.Value;

			if (position.Coordinate.Altitude != null)
				pos.Altitude = position.Coordinate.Altitude.Value;

			return pos;
		}
    }
}