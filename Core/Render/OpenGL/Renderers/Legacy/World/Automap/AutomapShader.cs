using GlmSharp;
using Helion.Geometry.Vectors;
using Helion.Render.OpenGL.Shader;

namespace Helion.Render.OpenGL.Renderers.Legacy.World.Automap;

public class AutomapShader : RenderProgram
{
    public AutomapShader() : base("Automap")
    {
    }

    public void Color(Vec3F color) => Uniforms.Set(color, "color");
    public void Mvp(mat4 mat) => Uniforms.Set(mat, "mvp");

    protected override string VertexShader() => @"
        #version 330

        layout(location = 0) in vec2 pos;

        uniform mat4 mvp;

        void main() {
            gl_Position = mvp * vec4(pos, 0.5, 1.0);
        }
    ";

    protected override string FragmentShader() => @"
        #version 330

        out vec4 fragColor;

        uniform vec3 color;

        void main() {
            fragColor = vec4(color, 1.0f);
        }
    ";
}
