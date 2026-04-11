using Phys;

internal static class FrameMath
{
    public static Vector3D GetFrameRight(Frame frame)
    {
        Vector3D right = new Vector3D(frame.TopLeft, frame.TopRight);
        return Normalize(right);
    }

    public static Vector3D GetFrameUp(Frame frame)
    {
        Vector3D up = new Vector3D(frame.BottomLeft, frame.TopLeft);
        return Normalize(up);
    }

    public static Vector3D GetFrameNormal(Frame frame)
    {
        Vector3D right = GetFrameRight(frame);
        Vector3D up = GetFrameUp(frame);
        return Normalize(Vector3D.Cross(right, up));
    }

    public static double GetFrameWidth(Frame frame)
    {
        return Length(new Vector3D(frame.TopLeft, frame.TopRight));
    }

    public static double GetFrameHeight(Frame frame)
    {
        return Length(new Vector3D(frame.BottomLeft, frame.TopLeft));
    }

    public static Point3D OffsetPoint(Point3D origin, Vector3D direction1, double amount1, Vector3D direction2, double amount2)
    {
        return new Point3D(
            origin.X + (direction1.X * amount1) + (direction2.X * amount2),
            origin.Y + (direction1.Y * amount1) + (direction2.Y * amount2),
            origin.Z + (direction1.Z * amount1) + (direction2.Z * amount2)
        );
    }

    private static double Length(Vector3D vector)
    {
        return Math.Sqrt((vector.X * vector.X) + (vector.Y * vector.Y) + (vector.Z * vector.Z));
    }

    private static Vector3D Normalize(Vector3D vector)
    {
        double len = Length(vector);
        if (len < 1e-12)
        {
            return new Vector3D();
        }

        return new Vector3D(vector.X / len, vector.Y / len, vector.Z / len);
    }
}
