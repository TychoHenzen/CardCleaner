using Godot;
using System;
using CardCleaner.Scripts;
using CardCleaner.Scripts.Controllers; // assumes Card and CardSignature live here

namespace CardCleaner.Scripts.Features.DeckBuilder
{
    [Tool]
    public partial class CardSlot : Node3D
    {
        [Export] public NodePath AreaPath;
        [Export] public float EjectForce = 2f;

        private Area3D _area;
        private RigidBody3D _card;

        [Signal]
        public delegate void CardChangedEventHandler();

        public override void _Ready()
        {
            _area = GetNode<Area3D>(AreaPath);
            _area.BodyEntered += OnBodyEntered;
        }

        private void OnBodyEntered(Node3D body)
        {
            if (body is not RigidBody3D card
                || !card.Name.ToString().StartsWith("Card"))
                return;

            if (_card == null)
            {
                LockCard(card);
                _card = card;
                EmitSignal(nameof(CardChanged));
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
            card.GlobalPosition = GlobalPosition;
            card.GlobalRotation = GlobalRotation;
        }

        private void EjectCard(RigidBody3D card)
        {
            card.Freeze = false;
            card.ApplyImpulse(Vector3.Up * EjectForce);
        }

        public bool HasCard => _card != null;

        public CardSignature ConsumeCardSignature()
        {
            if (_card == null)
                return null;
            var sig = _card.GetNode<CardController>(".").Signature;
            _card.QueueFree();
            _card = null;
            EmitSignal(nameof(CardChanged));
            return sig;
        }

        public void Clear()
        {
            if (_card != null)
                _card.QueueFree();
            _card = null;
            EmitSignal(nameof(CardChanged));
        }
    }
}