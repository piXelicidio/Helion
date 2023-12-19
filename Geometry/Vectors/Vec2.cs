using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Helion.Geometry.New.Interfaces;

namespace Helion.Geometry.New.Vectors;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Vec2 : 
    IVector2<float>,
    IAdditiveIdentity<Vec2, Vec2>,
    IMultiplicativeIdentity<Vec2, Vec2>,
    IUnaryNegationOperators<Vec2, Vec2>,
    IAdditionOperators<Vec2, Vec2, Vec2>,
    ISubtractionOperators<Vec2, Vec2, Vec2>,
    IMultiplyOperators<Vec2, Vec2, Vec2>,
    IMultiplyOperators<Vec2, float, Vec2>,
    IDivisionOperators<Vec2, Vec2, Vec2>,
    IDivisionOperators<Vec2, float, Vec2>,
    IEqualityOperators<Vec2, Vec2, bool>,
    ICreatable2<float, float, Vec2>,
    IConvertTo<Vec2>,
    IInverse<Vec2>,
    IMin<Vec2>,
    IMinGeneric<Vec2>,
    IMax<Vec2>,
    IMaxGeneric<Vec2>,
    IClamp<Vec2, Vec2, Vec2>,
    ISqrt<Vec2>,
    ILength<float>,
    IDistance<Vec2, float>,
    IDistanceGeneric<Vec2, float>,
    IUnit<Vec2>,
    INormalize,
    IDot2<Vec2, float>,
    ICross2<Vec2, float>,
    ILerp<Vec2, float, Vec2>,
    IComponent<Vec2, float>,
    IProject<Vec2, Vec2>
{
    internal const int Count = 2;
    
    public static int Dimension => Count;
    public static Vec2 Zero => new(0.0f, 0.0f);
    public static Vec2 One => new(1.0f, 1.0f);
    public static Vec2 AdditiveIdentity => Zero;
    public static Vec2 MultiplicativeIdentity => One;
    public static Vec2 MaxValue => new(float.MaxValue, float.MaxValue);
    public static Vec2 MinValue => new(float.MinValue, float.MinValue);

    private Vector2 m_vector;
    
    public float X { get => m_vector.X; set => m_vector.X = value; }
    public float Y { get => m_vector.Y; set => m_vector.Y = value; }
    public Vector2 Vector => m_vector;
    public float U => X;
    public float V => Y;

    public Vec2(in Vector2 vector)
    {
        m_vector = vector;
    }

    public Vec2(in float x, in float y)
    {
        m_vector = new(x, y);
    }

    public static implicit operator Vec2(in ValueTuple<float, float> tuple)
    {
        return new(tuple.Item1, tuple.Item2);
    }

    public void Deconstruct(out float x, out float y)
    {
        x = X;
        y = Y;
    }

    public float this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => m_vector[index];
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => m_vector[index] = value;
    }
    
    public static Vec2 operator -(Vec2 self) => new(-self.m_vector);
    public static Vec2 operator +(Vec2 self, Vec2 other) => new(self.m_vector + other.m_vector);
    public static Vec2 operator -(Vec2 self, Vec2 other) => new(self.m_vector - other.m_vector);
    public static Vec2 operator *(Vec2 self, Vec2 other) => new(self.m_vector * other.m_vector);
    public static Vec2 operator *(Vec2 self, float value) => new(self.m_vector * value);
    public static Vec2 operator *(float value, Vec2 self) => new(self.m_vector * value);
    public static Vec2 operator /(Vec2 self, Vec2 other) => new(self.m_vector / other.m_vector);
    public static Vec2 operator /(Vec2 self, float value) => new(self.m_vector / value);
    public static bool operator ==(Vec2 self, Vec2 other) => self.m_vector == other.m_vector;
    public static bool operator !=(Vec2 self, Vec2 other) => self.m_vector != other.m_vector;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Create(in float first, in float second) => new(first, second);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vec2 ConvertTo() => this;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vec2 Inverse() => new(1.0f / m_vector.X, 1.0f / m_vector.Y);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vec2 Min(in Vec2 other) => new(Vector2.Min(m_vector, other.m_vector));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vec2 Min<TOther>(in TOther other) where TOther : IConvertTo<Vec2> => new(Vector2.Min(m_vector, other.ConvertTo().m_vector));
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vec2 Max(in Vec2 other) => new(Vector2.Max(m_vector, other.m_vector));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vec2 Max<TOther>(in TOther other) where TOther : IConvertTo<Vec2> => new(Vector2.Max(m_vector, other.ConvertTo().m_vector));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vec2 Clamp(in Vec2 min, in Vec2 max) => new(Vector2.Clamp(m_vector, min.m_vector, max.m_vector));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vec2 Sqrt() => new(Vector2.SquareRoot(m_vector));
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float Length() => m_vector.Length();
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float LengthSquared() => m_vector.LengthSquared();
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float Distance(in Vec2 other) => Vector2.Distance(m_vector, other.m_vector);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float DistanceSquared(in Vec2 other) => Vector2.DistanceSquared(m_vector, other.m_vector);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float Distance<TOther>(in TOther other) where TOther : IConvertTo<Vec2> => Vector2.Distance(m_vector, other.ConvertTo().m_vector);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float DistanceSquared<TOther>(in TOther other) where TOther : IConvertTo<Vec2> => Vector2.DistanceSquared(m_vector, other.ConvertTo().m_vector);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vec2 Unit() => new(Vector2.Normalize(m_vector));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Normalize() => m_vector = Vector2.Normalize(m_vector);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float Dot(in Vec2 vec) => Vector2.Dot(m_vector, vec.m_vector);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float Dot<TOtherVec>(in TOtherVec vec) where TOtherVec : IVector2<float> => (m_vector.X * vec.X) + (m_vector.Y * vec.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Cross(in Vec2 other) => Vector2.Dot(m_vector, new(other.Y, -other.X));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Cross<TOtherVec>(in TOtherVec vec) where TOtherVec : IVector2<float> =>  (m_vector.X * vec.Y) - (m_vector.Y * vec.X);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vec2 Lerp(in Vec2 other, in float amount) => new(Vector2.Lerp(m_vector, other.m_vector, amount));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float Component(in Vec2 other) => Dot(in other) / other.Length();
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vec2 Project(in Vec2 other) => Dot(in other) / other.LengthSquared() * other;

    public override bool Equals(object? obj) => obj is Vec2 other && m_vector == other.m_vector;
    public override string ToString() => $"{X}, {Y}";
    public override int GetHashCode() => m_vector.GetHashCode();
}