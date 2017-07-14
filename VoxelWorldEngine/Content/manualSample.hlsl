
float4 manualSample(sampler Sampler, float2 UV, float2 textureSize)
{
    float2 texelpos = textureSize * UV;
    float2 lerps = frac(texelpos);
    float2 texelSize = 1.0 / textureSize;
    float4 sourcevals[4];
    sourcevals[0] = tex2D(Sampler, UV);
    sourcevals[1] = tex2D(Sampler, UV + float2(texelSize.x, 0));
    sourcevals[2] = tex2D(Sampler, UV + float2(0, texelSize.y));
    sourcevals[3] = tex2D(Sampler, UV + texelSize);
    float4 interpolated = lerp(
        lerp(sourcevals[0], sourcevals[1], lerps.x),
        lerp(sourcevals[2], sourcevals[3], lerps.x),
        lerps.y);
    return interpolated;
}

float3 decode(float3 enc)
{
    return 2 * enc.xyz - 1;
}

float3 encode(float3 n)
{
    n = normalize(n);
    n.xyz = 0.5 * (n.xyz + 1);
    return n;
}
