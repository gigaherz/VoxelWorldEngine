#include "manualSample.hlsl"

float4x4 InverseViewProjection;
float4x4 inverseView;
float3 CameraPosition;
float3 L;
float4 LightColor;
float LightIntensity;
float2 BufferTextureSize;

Texture ColorBuffer;
sampler s_ColorBuffer = sampler_state {
    texture = <ColorBuffer>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = clamp;
    AddressV = clamp;
};

Texture NormalBuffer;
sampler s_NormalBuffer = sampler_state {
    texture = <NormalBuffer>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = clamp;
    AddressV = clamp;
};

Texture DepthBuffer;
sampler s_DepthBuffer = sampler_state {
    texture = <DepthBuffer>;
    magfilter = POINT;
    minfilter = POINT;
    mipfilter = POINT;
    AddressU = clamp;
    AddressV = clamp;
};

Texture AlbedoBuffer;
sampler s_AlbedoBuffer = sampler_state {
    texture = <AlbedoBuffer>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = clamp;
    AddressV = clamp;
};

struct VSI
{
    float3 Position : POSITION0;
    float2 UV : TEXCOORD0;
};

struct  VSO
{
    float4 Position : POSITION0;
    float2 UV : TEXCOORD0;
};

VSO  VS(VSI input)
{
    VSO output;
    output.Position = float4(input.Position, 1);
    output.UV = input.UV;
    return output;
}

float4 Phong(float3 position, float3 N, float specularIntensity, float specularPower)
{
    float3 r = normalize(reflect(L, N));
    float3 e = normalize(CameraPosition - position.xyz);
    float nl = dot(N, -L);
    float3 diffuse = nl * LightColor.xyz;
    float specular = specularIntensity * pow(saturate(dot(r, e)), specularPower);
    return LightIntensity * float4(diffuse.rgb, specular);
}

float4 PS(VSO input) : COLOR0
{
    half4 encodedNormal = tex2D(s_NormalBuffer, input.UV);
    half3 normal = mul(decode(encodedNormal.xyz), inverseView);
    float4 albedo = tex2D(s_AlbedoBuffer, input.UV);
    float4 alpha = albedo.y;
    float specularIntensity = albedo.z;
    float specularPower = encodedNormal.w * 255;
    float depth = manualSample(s_DepthBuffer, input.UV, BufferTextureSize).x;
    float4 position = float4(input.UV.x * 2.0f - 1.0f, 1.0f - input.UV.y * 2.0f, depth, 1);
    position = mul(position, InverseViewProjection);
    position /= position.w;
    return Phong(position.xyz, normal, specularIntensity, specularPower) * alpha;
}
//Technique
technique Default
{
    pass p0
    {
        VertexShader = compile vs_4_0 VS();
        PixelShader = compile ps_4_0 PS();
    }
}