#include "manualSample.hlsl"

#define KERNEL_SIZE 32

// This constant removes artifacts caused by neighbour fragments with minimal depth difference.
#define CAP_MIN_DISTANCE 0.0001

// This constant avoids the influence of fragments, which are too far away.
#define CAP_MAX_DISTANCE 0.005

uniform float3 u_kernel[KERNEL_SIZE];

uniform float4x4 u_inverseProjectionMatrix;

float4x4 Projection;
float3 cornerFustrum;
float sampleRadius;
float distanceScale;
float2 rotationNoiseScale;
float2 BufferTextureSize;

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

float4 getViewPos(float2 texCoord)
{
    // Calculate out of the fragment in screen space the view space position.

    float x = texCoord.x * 2.0 - 1.0;
    float y = texCoord.y * 2.0 - 1.0;

    // Assume we have a normal depth range between 0.0 and 1.0
    float z = tex2D(s_DepthBuffer, texCoord).r * 2.0 - 1.0;

    float4 posProj = float4(x, y, z, 1.0);

    float4 posView = u_inverseProjectionMatrix * posProj;

    posView /= posView.w;

    return posView;
}

void main(void)
{
    // Calculate out of the current fragment in screen space the view space position.

    float4 posView = getViewPos(v_texCoord);

    // Normal gathering.

    float3 normalView = normalize(texture(s_NormalBuffer, v_texCoord).xyz * 2.0 - 1.0);

    // Calculate the rotation matrix for the kernel.

    float3 randomVector = normalize(texture(s_RandNormal, v_texCoord * rotationNoiseScale).xyz * 2.0 - 1.0);

    // Using Gram-Schmidt process to get an orthogonal vector to the normal vector.
    // The resulting tangent is on the same plane as the random and normal vector. 
    // see http://en.wikipedia.org/wiki/Gram%E2%80%93Schmidt_process
    // Note: No division by <u,u> needed, as this is for normal vectors 1. 
    float3 tangentView = normalize(randomVector - dot(randomVector, normalView) * normalView);

    float3 bitangentView = cross(normalView, tangentView);

    // Final matrix to reorient the kernel depending on the normal and the random vector.
    float3x3 kernelMatrix = float3x3(tangentView, bitangentView, normalView);

    // Go through the kernel samples and create occlusion factor.	
    float occlusion = 0.0;

    for (int i = 0; i < KERNEL_SIZE; i++)
    {
        // Reorient sample vector in view space ...
        float3 sampleVectorView = kernelMatrix * u_kernel[i];

        // ... and calculate sample point.
        float4 samplePointView = posView + sampleRadius * vec4(sampleVectorView, 0.0);

        // Project point and calculate NDC.

        float4 samplePointNDC = Projection * samplePointView;

        samplePointNDC /= samplePointNDC.w;

        // Create texture coordinate out of it.

        float2 samplePointTexCoord = samplePointNDC.xy * 0.5 + 0.5;

        // Get sample out of depth texture

        float zSceneNDC = texture(s_DepthBuffer, samplePointTexCoord).r * 2.0 - 1.0;

        float delta = samplePointNDC.z - zSceneNDC;

        // If scene fragment is before (smaller in z) sample point, increase occlusion.
        if (delta > CAP_MIN_DISTANCE && delta < CAP_MAX_DISTANCE)
        {
            occlusion += 1.0;
        }
    }

    // No occlusion gets white, full occlusion gets black.
    occlusion = 1.0 - occlusion / (float(KERNEL_SIZE) - 1.0);

    fragColor = vec4(occlusion, occlusion, occlusion, 1.0);
}

technique Default
{
    pass p0
    {
#if SM4
        VertexShader = compile vs_4_0 VS();
        PixelShader = compile ps_4_0 frag();
#else
        VertexShader = compile vs_3_0 VS();
        PixelShader = compile ps_3_0 frag();
#endif
    }
}