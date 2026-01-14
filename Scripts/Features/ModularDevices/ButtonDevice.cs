using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Devices;
using Godot;

namespace CardCleaner.Scripts.Features.ModularDevices;

public partial class ButtonDevice : Node3D, IDevice
{
    private readonly List<IJack> _jacks = [];
    private readonly Jack<Trigger> _triggerOutput;
    private readonly Jack<bool> _enableInput;

    private bool _enabled = true;

    public string UniqueId { get; set; } = Guid.NewGuid().ToString();
    public Node3D WorldNode => this;
    public IReadOnlyList<IJack> Jacks => _jacks;

    public IOutputJack<Trigger> TriggerOutput => _triggerOutput;
    public IInputJack<bool> EnableInput => _enableInput;
    public bool Enabled => _enabled;

    public ButtonDevice()
    {
        _triggerOutput = new Jack<Trigger>("TriggerOutput", JackDirection.Output);
        _enableInput = new Jack<bool>("EnableInput", JackDirection.Input);

        _enableInput.DataReceived += enabled => _enabled = enabled;

        _jacks.Add(_triggerOutput);
        _jacks.Add(_enableInput);
    }

    public IJack? GetJack(string name) => _jacks.FirstOrDefault(j => j.Name == name);

    public void Press()
    {
        if (_enabled)
            _triggerOutput.Emit(Trigger.Instance);
    }
}
