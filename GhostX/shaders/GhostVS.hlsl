
cbuffer MVPBuffer : register(b0)
{
    row_major float4x4 model;
    row_major float4x4 view;
    row_major float4x4 proj;
};

struct VSInput
{
    float3 pos   : POSITION;
    float4 color : COLOR;
};

struct PSInput
{
    float4 pos   : SV_POSITION;
    float4 color : COLOR;
};

PSInput VSMain(VSInput input)
{
    PSInput output;

    float4 worldPos = mul(model, float4(input.pos, 1.0f));
    float4 viewPos  = mul(view,  worldPos);
    output.pos      = mul(proj,  viewPos);

    output.color = input.color;
    return output;
}
