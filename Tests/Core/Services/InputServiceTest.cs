using CardCleaner.Scripts.Core.Services;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features;

[TestSuite]
[RequireGodotRuntime]
public class InputServiceTest
{
    private InputService _service= null!;
    private bool _actionTriggered;
    private bool _mouseActionPressed;
    private int _actionCallCount;

    [BeforeTest]
    public void Setup()
    {
        _service = new InputService();
        Assertions.AddNode(_service);
        _actionTriggered = false;
        _mouseActionPressed = false;
        _actionCallCount = 0;
    }
    [TestCase]
    public void TestRegisterKeyAction()
    {
        _service.RegisterAction(this, "test_action", Key.Space, () => _actionTriggered = true);
        
        Assertions.AssertThat(_service.GetKeyForAction("test_action")).IsEqual(Key.Space);
        Assertions.AssertBool(_service.IsActionPressed("test_action")).IsFalse();
    }

    [TestCase]
    public void TestRegisterMouseAction()
    {
        _service.RegisterAction(this, "test_mouse", MouseButton.Left, (pressed) => 
        {
            _mouseActionPressed = pressed;
            _actionCallCount++;
        });
        
        Assertions.AssertThat(_service.GetMouseButtonForAction("test_mouse")).IsEqual(MouseButton.Left);
    }

    [TestCase]
    public void TestUnregisterAction()
    {
        _service.RegisterAction(this, "test_action", Key.Space, () => _actionTriggered = true);
        
        // Unregister the action
        _service.UnregisterAction(this, "test_action");
        
        // Key mapping should be cleared
        Assertions.AssertThat(_service.GetKeyForAction("test_action")).IsEqual(Key.Unknown);
        Assertions.AssertBool(_service.IsActionPressed("test_action")).IsFalse();
    }

    [TestCase]
    public void TestUnregisterAllActions()
    {
        _service.RegisterAction(this , "action1", Key.Space, () => { });
        _service.RegisterAction(this, "action2", Key.Enter, () => { });
        
        _service.UnregisterAllActions(this);
        
        Assertions.AssertThat(_service.GetKeyForAction("action1")).IsEqual(Key.Unknown);
        Assertions.AssertThat(_service.GetKeyForAction("action2")).IsEqual(Key.Unknown);
    }

    [TestCase]
    public void TestRemapKeyAction()
    {
        _service.RegisterAction(this, "test_action", Key.Space, () => _actionTriggered = true);
        
        _service.RemapAction("test_action", Key.Enter);
        
        Assertions.AssertThat(_service.GetKeyForAction("test_action")).IsEqual(Key.Enter);
        Assertions.AssertThat(_service.GetMouseButtonForAction("test_action")).IsNull();
    }

    [TestCase]
    public void TestRemapMouseAction()
    {
        _service.RegisterAction(this, "test_action", MouseButton.Left, _ => { });
        
        _service.RemapAction("test_action", MouseButton.Right);
        
        Assertions.AssertThat(_service.GetMouseButtonForAction("test_action")).IsEqual(MouseButton.Right);
        Assertions.AssertThat(_service.GetKeyForAction("test_action")).IsEqual(Key.Unknown);
    }

    [TestCase]
    public void TestSimulateKeyPress()
    {
        _service.RegisterAction(this, "test_action", Key.Space, () => _actionTriggered = true);
        
        // Simulate key press event
        var keyEvent = new InputEventKey();
        keyEvent.Keycode = Key.Space;
        keyEvent.Pressed = true;
        keyEvent.Echo = false;
        
        _service._Input(keyEvent);
        
        Assertions.AssertBool(_actionTriggered).IsTrue();
        Assertions.AssertBool(_service.IsActionPressed("test_action")).IsTrue();
    }

    [TestCase]
    public void TestSimulateKeyRelease()
    {
        _service.RegisterAction(this, "test_action", Key.Space, () => _actionTriggered = true);
        
        // First press the key
        var pressEvent = new InputEventKey();
        pressEvent.Keycode = Key.Space;
        pressEvent.Pressed = true;
        pressEvent.Echo = false;
        _service._Input(pressEvent);
        
        // Reset flag
        _actionTriggered = false;
        
        // Then release the key
        var releaseEvent = new InputEventKey();
        releaseEvent.Keycode = Key.Space;
        releaseEvent.Pressed = false;
        releaseEvent.Echo = false;
        _service._Input(releaseEvent);
        
        Assertions.AssertBool(_actionTriggered).IsFalse(); // Release doesn't trigger action callback
        Assertions.AssertBool(_service.IsActionPressed("test_action")).IsFalse();
    }

    [TestCase]
    public void TestSimulateMousePress()
    {
        _service.RegisterAction(this, "test_mouse", MouseButton.Left, (pressed) => 
        {
            _mouseActionPressed = pressed;
            _actionCallCount++;
        });
        
        // Simulate mouse press
        var mouseEvent = new InputEventMouseButton();
        mouseEvent.ButtonIndex = MouseButton.Left;
        mouseEvent.Pressed = true;
        
        _service._Input(mouseEvent);
        
        Assertions.AssertBool(_mouseActionPressed).IsTrue();
        Assertions.AssertThat(_actionCallCount).IsEqual(1);
    }

    [TestCase]
    public void TestSimulateMouseRelease()
    {
        _service.RegisterAction(this, "test_mouse", MouseButton.Left, (pressed) => 
        {
            _mouseActionPressed = pressed;
            _actionCallCount++;
        });
        
        // Press then release
        var pressEvent = new InputEventMouseButton();
        pressEvent.ButtonIndex = MouseButton.Left;
        pressEvent.Pressed = true;
        _service._Input(pressEvent);
        
        var releaseEvent = new InputEventMouseButton();
        releaseEvent.ButtonIndex = MouseButton.Left;
        releaseEvent.Pressed = false;
        _service._Input(releaseEvent);
        
        Assertions.AssertBool(_mouseActionPressed).IsFalse(); // Last call was release
        Assertions.AssertThat(_actionCallCount).IsEqual(2); // Called for both press and release
    }

    [TestCase]
    public void TestJustPressedAndJustReleased()
    {
        _service.RegisterAction(this, "test_action", Key.Space, () => { });
        
        // Initially nothing should be just pressed
        Assertions.AssertBool(_service.IsActionJustPressed("test_action")).IsFalse();
        Assertions.AssertBool(_service.IsActionJustReleased("test_action")).IsFalse();
        
        // Simulate key press
        var pressEvent = new InputEventKey();
        pressEvent.Keycode = Key.Space;
        pressEvent.Pressed = true;
        pressEvent.Echo = false;
        _service._Input(pressEvent);
        
        Assertions.AssertBool(_service.IsActionJustPressed("test_action")).IsTrue();
        Assertions.AssertBool(_service.IsActionJustReleased("test_action")).IsFalse();
        
        // Process should clear just-pressed flags
        _service._Process(0.016); // Simulate one frame
        
        Assertions.AssertBool(_service.IsActionJustPressed("test_action")).IsFalse();
        
        // Now simulate release
        var releaseEvent = new InputEventKey();
        releaseEvent.Keycode = Key.Space;
        releaseEvent.Pressed = false;
        releaseEvent.Echo = false;
        _service._Input(releaseEvent);
        
        Assertions.AssertBool(_service.IsActionJustReleased("test_action")).IsTrue();
        Assertions.AssertBool(_service.IsActionPressed("test_action")).IsFalse();
    }

    [TestCase]
    public void TestMouseMovementEvent()
    {
        Vector2 lastMouseDelta = Vector2.Zero;
        _service.MouseMoved += (delta) => lastMouseDelta = delta;
        
        var mouseMotion = new InputEventMouseMotion();
        mouseMotion.Relative = new Vector2(10, -5);
        
        _service._Input(mouseMotion);
        
        Assertions.AssertThat(lastMouseDelta).IsEqual(new Vector2(10, -5));
    }

    [TestCase]
    public void TestKeyEchoIgnored()
    {
        _service.RegisterAction(this, "test_action", Key.Space, () => _actionCallCount++);
        
        // First press (not echo)
        var firstPress = new InputEventKey();
        firstPress.Keycode = Key.Space;
        firstPress.Pressed = true;
        firstPress.Echo = false;
        _service._Input(firstPress);
        
        // Echo press (should be ignored)
        var echoPress = new InputEventKey();
        echoPress.Keycode = Key.Space;
        echoPress.Pressed = true;
        echoPress.Echo = true;
        _service._Input(echoPress);
        
        Assertions.AssertThat(_actionCallCount).IsEqual(1); // Only first press counted
    }
}