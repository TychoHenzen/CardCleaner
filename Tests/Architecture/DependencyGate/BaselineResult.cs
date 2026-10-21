using System.Collections.Generic;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>
/// What the ratchet decided (#299): the failures the gate asserts empty, every lower-priority number that rose in a
/// step the decision accepted, which the gate prints as a report line, and the list notes, which are lines printed as
/// `dependency gate:` lines that never fail the gate (the class-name lists of NamedLists).
/// </summary>
internal sealed record BaselineResult(
    IReadOnlyList<string> Failures,
    IReadOnlyList<string> AcceptedRises,
    IReadOnlyList<string> ListNotes);
