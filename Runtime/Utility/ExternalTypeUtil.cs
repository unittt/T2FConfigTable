using UnityEngine;

namespace T2F.ConfigTable
{
    public static class ExternalTypeUtil
    {
        public static Vector2 NewVector2(vector2 v)
        {
            return new Vector2(v.X, v.Y);
        }

        public static Vector3 NewVector3(vector3 v)
        {
            return new Vector3(v.X, v.Y, v.Z);
        }
        
        public static Vector2Int NewVector2Int(vector2Int v)
        {
            return new Vector2Int(v.X, v.Y);
        }

        public static Vector3Int NewVector2Int(vector3Int v)
        {
            return new Vector3Int(v.X, v.Y, v.Z);
        }
        
        public static Color NewColor(color c)
        {
            return new Color(c.R, c.G, c.B, c.A);
        }
    }
}