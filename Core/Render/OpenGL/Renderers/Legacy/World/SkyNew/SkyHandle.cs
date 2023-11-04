namespace Helion.Render.OpenGL.Renderers.Legacy.World.SkyNew;

// A sky handle is a wrapper around an integer that is given out to callers
// when they want to treat a group of geometry as a group of triangles to
// be rendered with specific triangles.
//
// This is needed so we can support animated textures. We want to separate
// the geometry from the texture, because it is much more sane to bind the
// geometry and draw, but substitute the texture we want to render at runtime.
// 
// It is up to the caller to know how it wants to group the sky geometry, and
// then request a new handle to render with. An example would be as follows:
//
// Suppose there's four different kinds of skies in the map. The first is the
// standard SKY1, then SKY2, and then two wall-to-sky-texture line specials
// in the map. These give rise to four different types, and the user would
// request four handles from the sky renderer.
public readonly record struct SkyHandle(int Handle)
{
    public static implicit operator int (SkyHandle handle) => handle.Handle;
}