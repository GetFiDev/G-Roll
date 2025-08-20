Shader "Custom/StencilMask"
{
    SubShader
    {
        Tags { "Queue" = "Geometry+10" }

        Pass
        {
            Stencil
            {
                Ref 1
                Comp always
                Pass replace
            }
            ColorMask 0
        }
    }
}
