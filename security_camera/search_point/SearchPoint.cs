using Godot;


namespace YourGame.SecurityCamera
{
	public partial class SearchPoint : Node3D
	{
		[Export]
        private float _waitTime = 1;

        public float WaitTime{
            get {return _waitTime; }
        }

        public override void _Ready()
        {
            GetNode<MeshInstance3D>("MeshInstance3D").Hide();
        }
	}
}


