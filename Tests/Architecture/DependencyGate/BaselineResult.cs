using System.Collections.Generic;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>
/// What the ratchet decided (#299): the failures the gate asserts empty, and every lower-priority number that rose in
/// a step the decision accepted, which the gate prints as a report line.
/// </summary>
internal sealed record BaselineResult(IReadOnlyList<string> Failures, IReadOnlyList<string> AcceptedRises);
