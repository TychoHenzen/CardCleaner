using System;

namespace CardCleaner.Scripts.Core.Enum;

[Flags]
public enum SocketType
{
    Any = 1,
    Selected = 2,
    Transition = 4,
    Desert = 8,
    Forest =  16,
    Grasslands = 32,
    Mountains = 64,
    Ruins = 128,
    Swamp = 256,
}

