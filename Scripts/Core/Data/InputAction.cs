using System;
using Godot;

namespace CardCleaner.Scripts.Core.Services
{
    public class InputAction
    {
        public string Name { get; set; }
        public Key? Key { get; set; }
        public MouseButton? MouseButton { get; set; }
        public object Owner { get; set; }
        public Action Callback { get; set; }
        public Action<bool> MouseCallback { get; set; } // For mouse actions that need press/release
        
        public bool Matches(Key key) => Key.HasValue && Key.Value == key;
        public bool Matches(MouseButton button) => MouseButton.HasValue && MouseButton.Value == button;
    }
}