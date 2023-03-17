using Godot;
using System.Collections.Generic;
using System.Diagnostics;


namespace YourGame.SecurityCamera
{
	public partial class Camera : Node3D
	{
		/// Enums ///
		private enum State {Search, Track, Suspicious}

		/// Exported Fields ///
		[Export(PropertyHint.Range, "5,30,1")]
		private float _viewAngle = 15;
		[Export]
		private float _moveSpeedSearch = 1;
		[Export]
		private float _moveSpeedTrack = 3f;
		[Export]
		private bool _isSearchLoop = false;

		/// Fields - protected or private ///
		private State _currentState;
		private Marker3D _target;
		private MeshInstance3D _hub;
		private MeshInstance3D _cam;
		private SpotLight3D _spotLight;
		private RayCast3D _rayCast;
		private Area3D _detectionArea;
		private Node3D _playerInDetectionArea = null; // Retype to your player
		private Timer _timerSuspicious;
		private Tween _activeTween;
		private List<SearchPoint> _searchPoints = new List<SearchPoint>();
		private int _currentSearchPointTargetIdx = 0; // The point the camera will look at next
		private int _searchDirection = 1;
		private Color _colorSearch = Colors.Yellow;
		private Color _colorTrack = Colors.Red;
		private Color _colorSuspicious = Colors.Orange;


		//////////////////////////////
		// Engine Callback Methods  //
		//////////////////////////////
		public override void _Ready()
		{
			_target = GetNode<Marker3D>("Target");
			_hub = GetNode<MeshInstance3D>("Base/Hub");
			_cam = _hub.GetNode<MeshInstance3D>("Cam");
			_spotLight = _cam.GetNode<SpotLight3D>("Lense/SpotLight3D");
			_detectionArea = _cam.GetNode<Area3D>("Lense/Area3D");
			_rayCast = _cam.GetNode<RayCast3D>("Lense/RayCast3D");
			_timerSuspicious = GetNode<Timer>("TimerSuspicious");

			_spotLight.SpotAngle = _viewAngle;
			UpdateStatusColor(_colorSearch);

			_detectionArea.BodyEntered += OnDetectionAreaBodyEntered;
			_detectionArea.BodyExited += OnDetectionAreaBodyExited;
			_timerSuspicious.Timeout += OnTimerSuspiciousTimeout;

			_currentState = State.Search;

			if (!Engine.IsEditorHint())
			{
				foreach (Node child in GetChildren())
				{
					if (child is SearchPoint newPoint)
					{
						_searchPoints.Add(newPoint);
					}
				}
				
				_target.Position = _searchPoints[0].Position;
				MoveLookTargetToNextSearchPoint();
			}
		}

		public override void _PhysicsProcess(double delta)
		{
			bool _isPlayerVisible = IsPlayerVisible();

			switch (_currentState)
			{
				case State.Search:
					if(_isPlayerVisible)
					{
						TransitionToTrack();
					}
					break;

				case State.Track:
					if(!_isPlayerVisible)
					{
						TransitionToSuspicious();
					}
					else
					{
		 				MoveLookTargetTowardPlayer((float)delta);
						// Notify other game objects of player position here with signal or some other method for updates every physics frame
					}
					break;	
				
				case State.Suspicious:
					if (_isPlayerVisible)
					{
						_timerSuspicious.Stop();
						TransitionToTrack();
					}
					break;
				
				default: break;
			}

			PointAtTarget((float)delta);
		}


		//////////////////////////////
		// Signal Connected Methods //
		//////////////////////////////
		private void OnDetectionAreaBodyEntered(Node3D body) // Configure collision layers to only detect player
		{
			//Debug.Assert(body is Player, "Security camera should only detect the player");
			_playerInDetectionArea = body;
			_rayCast.Enabled = true;
		}

		private void OnDetectionAreaBodyExited(Node3D body)  // Configure collision layers to only detect player
		{
			//Debug.Assert(body is Player, "Security camera should only detect the player");
			_playerInDetectionArea = null;
			_rayCast.Enabled = false;
		}

		private void OnTimerSuspiciousTimeout()
		{
			TransitionToSearch();
		}


		//////////////////////////////
		//	  Private Methods	 //
		//////////////////////////////
		private void PointAtTarget(float delta)
		{
			Vector2 targetPosXZ = new Vector2(_target.Position.X, _target.Position.Z);
			Vector2 forwardDir = new Vector2(0, 1);
			float hubAngle = forwardDir.AngleTo(targetPosXZ);
			
			_hub.Rotation = new Vector3(0, -hubAngle, 0);
			_cam.LookAt(_target.GlobalPosition);
		}

		private bool IsPlayerVisible()
		{
			if (_playerInDetectionArea == null) return false;

			Vector3 playerRelativePosition = _rayCast.ToLocal(_playerInDetectionArea.GlobalPosition);
			float viewAngle = Vector3.Forward.AngleTo(playerRelativePosition);
			bool isPlayerInFOV = viewAngle <= Mathf.DegToRad(_viewAngle);
			if (!isPlayerInFOV) return false;

			// CONSIDER: Do multiple raycasts to differeent parts of player to determine visibility
			_rayCast.TargetPosition = _rayCast.ToLocal(_playerInDetectionArea.GlobalPosition + new Vector3(0, 0.5f, 0)); // So not casting to feet.
			_rayCast.ForceRaycastUpdate();

			return !_rayCast.IsColliding();
		}

		private void UpdateStatusColor(Color newColor)
		{
			_spotLight.LightColor = newColor;
			_cam.SetInstanceShaderParameter("color", newColor);
		}

		private void TransitionToSearch()
		{
			if (_activeTween != null) _activeTween.Kill();
			UpdateStatusColor(_colorSearch);
			_currentState = State.Search;
			MoveLookTargetToNextSearchPoint();
		}

		private void TransitionToTrack()
		{
			if (_activeTween != null) _activeTween.Kill();
			UpdateStatusColor(_colorTrack);
			_currentState = State.Track;
		}

		private void TransitionToSuspicious()
		{
			if (_activeTween != null) _activeTween.Kill();
			UpdateStatusColor(_colorSuspicious);
			_currentState = State.Suspicious;
			_timerSuspicious.Start();
		}

		private void MoveLookTargetToNextSearchPoint()
		{
			if (_isSearchLoop)
			{
				_currentSearchPointTargetIdx += _searchDirection;
				if (_currentSearchPointTargetIdx == _searchPoints.Count)
				{
					_currentSearchPointTargetIdx = 0;
				}
			}
			else
			{
				if (_currentSearchPointTargetIdx + _searchDirection == _searchPoints.Count)
				{
					_searchDirection = -1;
				}
				else if ( _currentSearchPointTargetIdx + _searchDirection == -1)
				{
					_searchDirection = 1;
				}
				_currentSearchPointTargetIdx += _searchDirection;
			}
			
			Vector3 _positionTarget = _searchPoints[_currentSearchPointTargetIdx].Position;
			float _translationLength = _target.Position.DistanceTo(_positionTarget);
			float panTime = _translationLength / _moveSpeedSearch;

			_activeTween = CreateTween();
			_activeTween.TweenProperty(_target, "position", _positionTarget, panTime);
			_activeTween.TweenInterval(_searchPoints[_currentSearchPointTargetIdx].WaitTime);
			_activeTween.Finished += MoveLookTargetToNextSearchPoint;
			_activeTween.Play();
		}

		private void MoveLookTargetTowardPlayer(float delta)
		{
			Vector3 newPos = _target.GlobalPosition.MoveToward(_playerInDetectionArea.GlobalPosition, _moveSpeedTrack * delta);
			_target.GlobalPosition = newPos;
		}
	}
}

