#include "manualSample.hlsl"

float4 ClearColor;

float4 VS(float3 Position : POSITION0) : POSITION0 
{ 
    return float4(Position, 1); 
} 

struct PSO 
{ 
    float4 Color : COLOR0; 
    float4 Normals : COLOR1; 
    float4 Depth : COLOR2;
    float4 Albedo : COLOR3;
}; 


PSO PS() 
{ 
    PSO output;
    output.Albedo = 0;
    output.Color = float4(ClearColor.rgb, 0);
    output.Normals.xyz = 0.5f;
    output.Normals.w = 0.0f;
    output.Depth = 1.0f;
    return output;
} 

technique Default 
{ 
    pass p0 
    { 
        VertexShader = compile vs_4_0 VS(); 
        PixelShader = compile ps_4_0 PS(); 
    } 
} 