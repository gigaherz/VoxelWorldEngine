#include "manualSample.hlsl"


float4x4 Projection;
float3 cornerFustrum;
float sampleRadius;
float bias;
float2 BufferTextureSize;
float3 sampleSphere[64];
#define NUM_SAMPLES 64

Texture RandNormal;
sampler s_RandNormal = sampler_state {
    texture = <RandNormal>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
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
    float3 ViewDirection : TEXCOORD1;
};

VSO VS(VSI input)
{
    VSO output;
    output.Position = float4(input.Position, 1);
    output.UV = input.UV;
    output.ViewDirection = float3(
        -cornerFustrum.x * input.Position.x,
         cornerFustrum.y * input.Position.y,
         cornerFustrum.z);
    return output;
}

float4 PS(VSO input) : COLOR0
{
    float4 viewPos = tex2D(s_DepthBuffer, input.UV);
    float4 fragPos = mul(viewPos * float4(1,-1,1,1), Projection);
    fragPos = fragPos / fragPos.w;
    //return float4(0.5 + 0.5 * fragPos.xy, 0, 1);
    //return float4(fragPos.z, -fragPos.z, 0, 1);
    //return float4(1-fragPos.z, 0, 0, 1);

    float3 viewDirection = normalize(input.ViewDirection);
    float3 randNormal = tex2D(s_RandNormal, input.UV * 200.0f).xyz;
    float3 normal = decode(tex2D(s_NormalBuffer, input.UV).xyz);

    float radius = sampleRadius; // / -viewPos.z;
    //return float4(radius, radius*10, radius*0.1, 1.0);

    float3 tangent = normalize(randNormal - normal * dot(randNormal, normal));
    float3 bitangent = cross(normal, tangent);
    float3x3 TBN = float3x3(tangent, bitangent, normal);

    float occlusion = 0.0f;
    for (int i = 0; i < NUM_SAMPLES; i++)
    {
        //float3 dir = reflect(sampleSphere[i], randNormal) * radius;
        float3 dir = mul(TBN, sampleSphere[i]) * radius;

        float4 samplePos = float4(viewPos.xyz + dir, 1.0f);
        float4 offset = mul(samplePos * float4(1, -1, 1, 1), Projection);
        offset = offset / offset.w;
        float2 sampleTexCoord = 0.5 + 0.5 * offset.xy;
        //return float4(sampleTexCoord, 0, 1);

        float4 sampleDepths = tex2D(s_DepthBuffer, sampleTexCoord);
        sampleDepths /= sampleDepths.w;
        //return float4(sampleDepths.xy, 0, 1);
        //return float4(sampleDepths.z, -sampleDepths.z, 0, 1);

        //float test = (samplePos.z - sampleDepths.z + distanceScale);
        //return float4(test, -test, 0, 1);

        //float rangeCheck = abs(viewPos.z - sampleDepths.z) < radius ? 1.0 : 0.0;
        float rangeCheck = smoothstep(0.0, 1.0, radius / abs(viewPos.z - sampleDepths.z));
        //return float4(samplePos.z-sampleDepths.z, sampleDepths.z - samplePos.z, 0, 1);

        occlusion += (sampleDepths.z >= samplePos.z + bias) ? rangeCheck : 0.0;
    }
    occlusion = occlusion / NUM_SAMPLES;

    return fragPos.z < 0 ? float4(1,1,1,1) : float4(1.0 - occlusion.xxx, 1.0f);
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