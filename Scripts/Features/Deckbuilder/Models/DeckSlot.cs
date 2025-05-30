using System.Collections.Generic;
using CardCleaner.Scripts.Controllers;
using Godot;


namespace CardCleaner.Scripts.Features.Deckbuilder.Models
{
    [Tool]
    public partial class DeckSlot : Node3D
    {
        [Export] public int Capacity { get; set; } = 5;
        [Export] public NodePath AreaPath;
        [Export] public float EjectForce = 2f;
        [Export] public Vector3 StackOffset = new(0, 0.02f, 0);

        private Area3D _area;
        private readonly List<RigidBody3D> _cards = new();

        [Signal]
        public delegate void CardsChangedEventHandler();

        public override void _Ready()
        {
            _area = GetNode<Area3D>(AreaPath);
            _area.BodyEntered += OnBodyEntered;
        }

        private void OnBodyEntered(Node3D body)
        {
            if (body is not RigidBody3D card 
                || _cards.Contains(card) 
                || !card.Name.ToString().StartsWith("Card"))
                return;

            if (_cards.Count < Capacity)
            {
                LockCard(card);
                _cards.Add(card);
                EmitSignal(nameof(CardsChanged));
            }
            else
            {
                EjectCard(card);
            }
        }

        private void LockCard(RigidBody3D card)
        {
            card.Freeze = true;
            card.LinearVelocity = Vector3.Zero;
            card.AngularVelocity = Vector3.Zero;
            AddChild(card);
            card.GlobalPosition = GlobalPosition + StackOffset * _cards.Count;
            card.GlobalRotation = GlobalRotation;
        }

        private void EjectCard(RigidBody3D card)
        {
            card.Freeze = false;
            card.ApplyImpulse(Vector3.Up * EjectForce);
        }

        public bool HasCards => _cards.Count > 0;

        public List<CardSignature> ConsumeAllCardSignatures()
        {
            var sigs = new List<CardSignature>();
            foreach (var c in _cards)
            {
                sigs.Add(c.GetNode<CardController>(".").Signature);
                c.QueueFree();
            }

            _cards.Clear();
            EmitSignal(nameof(CardsChanged));
            return sigs;
        }

        public void Clear()
        {
            foreach (var c in _cards)
                c.QueueFree();
            _cards.Clear();
            EmitSignal(nameof(CardsChanged));
        }
    }
}