#include "manualSample.hlsl"

#define NUMSAMPLES 8

float4x4 Projection;
float3 cornerFustrum;
float sampleRadius;
float distanceScale;
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

float4 PS0(VSO input) : COLOR0
{
    float4 samples[8] = {
        float4( 0.355512, -0.709318, -0.102371,  0.0),
        float4( 0.534186,  0.71511,  -0.115167,  0.0),
        float4(-0.87866,   0.157139, -0.115167,  0.0),
        float4( 0.140679, -0.475516, -0.0639818, 0.0),
        float4(-0.207641,  0.414286,  0.187755,  0.0),
        float4(-0.277332, -0.371262,  0.187755,  0.0),
        float4( 0.63864,  -0.114214,  0.262857,  0.0),
        float4(-0.184051,  0.622119,  0.262857,  0.0)
    };

    float3 viewDirection = normalize(input.ViewDirection);
    float depth = tex2D(s_DepthBuffer, input.UV).g;
    float3 se = depth * viewDirection;
    float3 randNormal = tex2D(s_RandNormal, input.UV * 200.0f).xyz;
    float3 normal = decode(tex2D(s_NormalBuffer, input.UV).xyz);
    float finalColor = 0.0f;

    for (int i = 0; i < NUMSAMPLES; i++)
    {
        float3 ray = reflect(samples[i].xyz, randNormal) * sampleRadius;

        if (dot(ray, normal) < 0)
            ray += normal * sampleRadius;
        
        float4 sampl = float4(se + ray, 1.0f);
        float4 ss = mul(sampl, Projection);
        ss /= ss.w;
        float2 sampleTexCoord = 0.5f * (ss.xy + 1);
        float sampleDepth = tex2D(s_DepthBuffer, sampleTexCoord).g;
        if (sampleDepth > 0.99 || sampleDepth > ss.z)
        {
            finalColor++;
        }
        else
        {
            float occlusion = distanceScale * max(sampleDepth - depth, 0.0f);
            finalColor += 1.0f / (1.0f + occlusion * occlusion * 0.1);
        }
    }

    return float4(finalColor / NUMSAMPLES,
                  finalColor / NUMSAMPLES,
                  finalColor / NUMSAMPLES,
                  1.0f);
}

//Count of SSAO samples. 0-16, because sampleSphere has only 16 elements
#define NUM_SAMPLES 16

//Random samplekernel
const float3 sampleSphere[] = {
    float3(0.2024537f, 0.841204f, -0.9060141f),
    float3(-0.2200423f, 0.6282339f,-0.8275437f),
    float3(0.3677659f, 0.1086345f,-0.4466777f),
    float3(0.8775856f, 0.4617546f,-0.6427765f),
    float3(0.7867433f,-0.141479f, -0.1567597f),
    float3(0.4839356f,-0.8253108f,-0.1563844f),
    float3(0.4401554f,-0.4228428f,-0.3300118f),
    float3(0.0019193f,-0.8048455f, 0.0726584f),
    float3(-0.7578573f,-0.5583301f, 0.2347527f),
    float3(-0.4540417f,-0.252365f, 0.0694318f),
    float3(-0.0483353f,-0.2527294f, 0.5924745f),
    float3(-0.4192392f, 0.2084218f,-0.3672943f),
    float3(-0.8433938f, 0.1451271f, 0.2202872f),
    float3(-0.4037157f,-0.8263387f, 0.4698132f),
    float3(-0.6657394f, 0.6298575f, 0.6342437f),
    float3(-0.0001783f, 0.2834622f, 0.8343929f),
};

float4 PS1(VSO input) : COLOR0
{
    const float samplerRadius = 0.00005f;
    const float strength = 1.0f;
    const float totalStrength = 3.0f;
    const float falloffMin = 0.00001f;
    const float falloffMax = 0.006f;

    float3 randNormal = tex2D(s_RandNormal, input.UV * 200.0f).xyz;
    float depth = tex2D(s_DepthBuffer, input.UV).g;
    float3 normal = tex2D(s_NormalBuffer, input.UV).xyz;

    //We are working in 2D-Space, so we have to scale the
    //radius by distance, to keep the illusion of a depth scene.
    float radius = samplerRadius / depth;

    float3 centerPos = float3(input.UV, depth);
    float occ = 0.0f;
    for (unsigned int i = 0; i < NUM_SAMPLES; ++i)
    {
        //Reflect sample to random direction.
        float3 offset = reflect(sampleSphere[i], randNormal);

        //Convert to hemisphere.
        offset = sign(dot(offset, normal))*offset;

        //Invert hemisphere on the Y-Axis. The texture coordinate increases
        //from top to buttom, the view normal points opposite,
        //without inverting the hemisphere points inside the floor, of course you could also
        //invert the normal buffer's y-axis instead.
        offset.y = -offset.y;

        //Ray, relative to the current texcoord and scaled using the SSAO radius.
        float3 ray = centerPos + radius*offset;

        //Skip rays outside the screen
        if ((saturate(ray.x) == ray.x) && (saturate(ray.y) == ray.y))
        {

            //Get linear depth at ray.xy
            float occDepth = tex2D(s_DepthBuffer, ray.xy).g;

            //Get viewspace normal at ray.xy.
            float3 occNormal = tex2D(s_NormalBuffer, ray.xy).xyz;

            //Difference between depth and occluder depth.
            float depthDifference = (centerPos.z - occDepth);

            float normalDifference = dot(occNormal, normal);

            //Occlusion dependet on angle between normals, smaller angle causes more occlusion.
            float normalOcc = 1.0 - saturate(normalDifference);

            //Occlusion dependet on depth difference, limited using falloffMin and falloffMax.
            //helps to reduce self occlusion and halo-artifacts. Try a bit around with falloffMin/Max.
            float depthOcc = step(falloffMin, depthDifference)*
                (1.0f - smoothstep(falloffMin, falloffMax, depthDifference));

            //Take all occlusion factors together and scale them using the per step strength.
            occ += saturate(depthOcc*normalOcc*strength);
        }
    }

    //Divide by number of samples to get the average occlusion.
    occ /= NUM_SAMPLES;

    //Invert the result and potentiate it using the total strength.
    return saturate(pow(1.0f - occ, totalStrength));
}

float4 PS2(VSO input) : COLOR0
{
    // tile noise texture over screen, based on screen dimensions divided by noise size
    float2 noiseScale = float2(BufferTextureSize.x / 4.0, BufferTextureSize.y / 4.0);

    float3 randomVec = tex2D(s_RandNormal, input.UV * noiseScale).xyz;
    float depth = tex2D(s_DepthBuffer, input.UV).g;
    float3 normal = tex2D(s_NormalBuffer, input.UV).xyz;

    //We are working in 2D-Space, so we have to scale the
    //radius by distance, to keep the illusion of a depth scene.
    float radius = sampleRadius / depth;

    float3 viewDirection = normalize(input.ViewDirection);
    float3 fragPos = depth * viewDirection;

    float3 tangent = normalize(randomVec - normal * dot(randomVec, normal));
    float3 bitangent = cross(normal, tangent);
    float3x3 TBN = float3x3(tangent, bitangent, normal);

    float occlusion = 0.0f;
    for (int i = 0; i < NUM_SAMPLES; ++i)
    {
        // get sample position
        float3 samplePos = mul(sampleSphere[i], TBN); // from tangent to view-space
        samplePos = fragPos + samplePos * radius;
        
        float4 offset = float4(samplePos, 1.0f);
        offset = mul(offset, Projection);                 // from view to clip-space
        offset.xyz /= offset.w;                           // perspective divide
        offset = float4(offset.xyz * 0.5f + 0.5f, 1.0);   // transform to range 0.0 - 1.0  

        float sampleDepth = tex2D(s_DepthBuffer, offset.xy).g;

        if (sampleDepth >= samplePos.z + distanceScale)
        {
            float rangeCheck = smoothstep(0.0f, 1.0f, radius / abs(depth - sampleDepth));
            occlusion += rangeCheck;
        }
    }

    occlusion = 1.0 - (occlusion / NUM_SAMPLES);

    return float4(occlusion, occlusion, occlusion, 1.0f);
}

technique Default
{
    pass p0
    {
#if SM4
        VertexShader = compile vs_4_0 VS();
        PixelShader = compile ps_4_0 PS2();
#else
        VertexShader = compile vs_3_0 VS();
        PixelShader = compile ps_3_0 PS2();
#endif
    }
}