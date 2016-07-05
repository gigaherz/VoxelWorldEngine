float4x4 World;
float4x4 View;
float4x4 Projection;

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
};

VS_out VS_main(VS_in input)
{
    VS_out output;

    float4 worldPosition = mul(input.Position, World);
    float4 viewPosition = mul(worldPosition, View);
    output.Position = mul(viewPosition, Projection);
    output.Normal = input.Normal;
    output.Color = input.Color;
    output.TexCoord0 = input.TexCoord0;

    return output;
}

float4 PS_main(VS_out input) : COLOR0
{
    float3 normal = float3(input.Normal.x,1,1);
    float3 lightdirection = normalize(float3(2,-5,1));

    float incidence = 0.7 - 0.3 * dot(normal, lightdirection);

    float4 color = input.Color * tex2D(sampler0, input.TexCoord0);

    return float4(color.rgb * incidence, color.a);
}

technique Technique1
{
    pass Pass1
    {
#ifdef SM4
        VertexShader = compile vs_4_0 VS_main();
        PixelShader  = compile ps_4_0 PS_main();
#else
        VertexShader = compile vs_3_0 VS_main();
        PixelShader = compile ps_3_0 PS_main();
#endif
    }
}
