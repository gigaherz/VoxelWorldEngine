#include "manualSample.hlsl"

float2 BlurDirection;
float2 TargetSize;

Texture SSAO;
sampler s_SSAO = sampler_state {
    texture = <SSAO>;
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

struct VSI
{
    float3 Position : POSITION0;
    float2 UV : TEXCOORD0;
};

struct VSO
{
    float4 Position : POSITION0;
    float2 UV : TEXCOORD0;
};

VSO VS(VSI input)
{
    VSO output;
    output.Position = float4(input.Position, 1);
    output.UV = input.UV;
    return output;
}

float4 PS(VSO input) : COLOR0
{
    float3 normal = decode(tex2D(s_NormalBuffer, input.UV).xyz);
    float depth = manualSample(s_DepthBuffer, input.UV, TargetSize).y;

    float ssaoAccumulator = tex2D(s_SSAO, input.UV).x;
    float ssaoScale = 1;

    float2 dir = BlurDirection / TargetSize;

    int blurRadius = 4;

    for (int i = -blurRadius; i <= blurRadius; i++)
    {
        float2 newUV = input.UV + i * dir;

        float _sample = tex2D(s_SSAO, newUV).x;

        float3 samplenormal = decode(tex2D(s_NormalBuffer, newUV).xyz);

        if (dot(samplenormal, normal) > 0.99)
        {
            float contribution = blurRadius - abs(i);
            ssaoScale += contribution;
            ssaoAccumulator.x += _sample * contribution;
        }
    }

    return float4(ssaoAccumulator.xxx / ssaoScale, 1);
}

technique Default
{
    pass p0
    {
        VertexShader = compile vs_4_0 VS();
        PixelShader = compile ps_4_0 PS();
    }
}