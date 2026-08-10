struct PSInput
{
    float4 pos   : SV_POSITION;
    float4 color : COLOR;
};

float4 PSMain(PSInput input) : SV_TARGET
{
    return input.color;
}
