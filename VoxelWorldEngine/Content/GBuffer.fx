#include "manualSample.hlsl"

float4x4 World;
float4x4 View;
float4x4 Projection;
float4x4 WorldViewIT;

texture Texture;
texture NormalMap;
texture SpecularMap;

sampler AlbedoSampler = sampler_state 
{ 
    texture = <Texture>; 
    MINFILTER = LINEAR; 
    MAGFILTER = LINEAR; 
    MIPFILTER = LINEAR; 
    ADDRESSU = WRAP; 
    ADDRESSV = WRAP; 
}; 

sampler NormalSampler = sampler_state 
{ 
    texture = <NormalMap>;
    MINFILTER = LINEAR; 
    MAGFILTER = LINEAR; 
    MIPFILTER = LINEAR; 
    ADDRESSU = WRAP; 
    ADDRESSV = WRAP; 
}; 

sampler SpecularSampler = sampler_state 
{ 
    texture = <SpecularMap>; 
    MINFILTER = LINEAR; 
    MAGFILTER = LINEAR; 
    MIPFILTER = LINEAR; 
    ADDRESSU = WRAP; 
    ADDRESSV = WRAP; 
}; 

struct VSI 
{ 
    float4 Position : POSITION0; 
    float3 Normal : NORMAL0;
    float2 UV : TEXCOORD0;
    float3 Tangent : TANGENT0; 
    float3 BiTangent : BINORMAL0; 
}; 

struct VSO 
{ 
    float4 Position : POSITION0; 
    float2 UV : TEXCOORD0;
    float3 Depth : TEXCOORD1; 
    float3x3 TBN : TEXCOORD2; 
}; 

struct PSO 
{
    float4 Color   : COLOR0;
    float4 Normals : COLOR1;
    float4 Depth   : COLOR2;
    float4 Albedo  : COLOR3;
}; 

VSO VS(VSI input) 
{ 
    VSO output;

    float4 worldPosition = mul(input.Position, World); 
    float4 viewPosition = mul(worldPosition, View); 
    output.Position = mul(viewPosition, Projection); 

    output.Depth.x = output.Position.z; 
    output.Depth.y = output.Position.w; 
    output.Depth.z = viewPosition.z; 

    output.TBN[0] = normalize(mul(input.Tangent, (float3x3)WorldViewIT)); 
    output.TBN[1] = normalize(mul(input.BiTangent, (float3x3)WorldViewIT)); 
    output.TBN[2] = normalize(mul(input.Normal, (float3x3)WorldViewIT)); 

    output.UV = input.UV; 

    return output; 
} 

PSO PS(VSO input) 
{ 
    PSO output;

    float4 color = tex2D(AlbedoSampler, input.UV);

    float brightness = (color.r * 4 + color.g * 7 + color.b * 5) / 16.0;
    float shininess = 0;

    output.Color = color;
    output.Albedo = float4(brightness.xxx, shininess);

    //Pass Extra - Can be whatever you want, in this case will be a Specular Value 
    output.Albedo.w = tex2D(SpecularSampler, input.UV).x;

#if 0
    half3 normal = tex2D(NormalSampler, input.UV).xyz * 2.0f - 1.0f; 
    normal = normalize(mul(normal, input.TBN)); 
    output.Normals.xyz = encode(normal); 
#else
    //Pass this instead to disable normal mapping 
    output.Normals.xyz = encode(normalize(input.TBN[2]));
#endif

    output.Normals.w = tex2D(SpecularSampler, input.UV).y; 

    output.Depth = input.Depth.x / input.Depth.y; 
    output.Depth.g = input.Depth.z; 

    return output; 
} 

technique Default 
{ 
    pass p0 
    {
#if SM4
        VertexShader = compile vs_4_0 VS();
        PixelShader = compile ps_4_0 PS();
#else
        VertexShader = compile vs_3_0 VS();
        PixelShader = compile ps_3_0 PS();
#endif
    } 
} 