#include "manualSample.hlsl"

float4x4 World;
float4x4 View;
float4x4 Projection;
float4x4 WorldViewIT;

Texture Texture0;
sampler sampler0 = sampler_state {
    texture = <Texture0> ;
    magfilter = POINT;
    minfilter = POINT; 
    mipfilter = POINT; 
    AddressU = wrap; 
    AddressV = wrap;
};

struct VS_in
{
    float4 Position  : SV_Position;
    float3 Normal    : NORMAL0;
    float4 Color     : COLOR0;
    float2 TexCoord0 : TEXCOORD0;
};

struct VS_out
{
    float4 Position  : SV_Position;
    float2 TexCoord0 : TEXCOORD0;
    float3 Normal    : TEXCOORD1;
    float4 Color     : TEXCOORD2;
    float3 Depth     : TEXCOORD3;
};

struct PS_out
{
    float4 Color  : COLOR0;
    float4 Normal : COLOR1;
    float4 Depth  : COLOR2;
    float4 Albedo : COLOR3;
};

VS_out VS_main(VS_in input)
{
    VS_out output;

    float4 worldPosition = mul(input.Position, World);
    float4 viewPosition = mul(worldPosition, View);
    output.Position = mul(viewPosition, Projection);
    output.Normal = normalize(mul(input.Normal, (float3x3)WorldViewIT));
    output.Color = input.Color;
    output.TexCoord0 = input.TexCoord0;

    output.Depth.x = output.Position.z;
    output.Depth.y = output.Position.w;
    output.Depth.z = viewPosition.z;

    return output;
}

PS_out PS_main(VS_out input)
{
    PS_out output;

    float4 color = input.Color * tex2D(sampler0, input.TexCoord0);

    output.Color = color;
    output.Normal = float4(encode(input.Normal), 1);
    output.Depth = input.Depth.x / input.Depth.y;
    output.Depth.g = input.Depth.z;

    float brightness = (color.r * 4 + color.g * 7 + color.b * 5) / 16.0;
    float opacity = 1; // do not process light transparency yet.
    float shininess = 0;

    output.Albedo = float4(brightness, opacity, shininess, 1);

    return output;
}

technique Technique
{
    pass Pass
    {
#if SM4
        VertexShader = compile vs_4_0 VS_main();
        PixelShader  = compile ps_4_0 PS_main();
#else
        VertexShader = compile vs_3_0 VS_main();
        PixelShader = compile ps_3_0 PS_main();
#endif
    }
}
