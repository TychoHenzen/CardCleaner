using System;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>The range of asking prices the player can set on anything the shop sells.</summary>
public static class ShopPrice
{
    public const int Min = 1;

    public const int Max = 9999;

    /// <summary>The price moved into [<see cref="Min" />, <see cref="Max" />].</summary>
    public static int Clamp(long price) => (int)Math.Clamp(price, Min, Max);
}
