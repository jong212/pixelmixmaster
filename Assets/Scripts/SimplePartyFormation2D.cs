using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class SimplePartyFormation2D
{
    public static Vector2[] GetOffsets(int count)
    {
        float side = 0.9f;
        float back = 1.2f;

        switch (count)
        {
            case 1:
                return new[] { new Vector2(0, -back) };

            case 2:
                return new[]
                {
                    new Vector2(-side, -back),
                    new Vector2( side, -back)
                };

            case 3:
                return new[]
                {
                    new Vector2(-side, -back),
                    new Vector2( side, -back),
                    new Vector2(0,    -back * 2f)
                };

            default:
                return new Vector2[0];
        }
    }
}
