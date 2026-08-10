// GhostPipeline.cpp
#include "../include/GhostPipeline.h"
#include "../include/GhostMath.h"
#include <d3d12.h>
#include <d3dcompiler.h>
#include <wrl/client.h>
using Microsoft::WRL::ComPtr;

// ============================================================
//  Geometry data
// ============================================================

static GhostVertex CubeVertices[] =
{
    { -0.5f,  0.5f, -0.5f,  1,0,0,1 }, {  0.5f,  0.5f, -0.5f,  1,0,0,1 },
    {  0.5f, -0.5f, -0.5f,  1,0,0,1 }, { -0.5f, -0.5f, -0.5f,  1,0,0,1 },
    {  0.5f,  0.5f,  0.5f,  0,1,0,1 }, { -0.5f,  0.5f,  0.5f,  0,1,0,1 },
    { -0.5f, -0.5f,  0.5f,  0,1,0,1 }, {  0.5f, -0.5f,  0.5f,  0,1,0,1 },
    { -0.5f,  0.5f,  0.5f,  0,0,1,1 }, {  0.5f,  0.5f,  0.5f,  0,0,1,1 },
    {  0.5f,  0.5f, -0.5f,  0,0,1,1 }, { -0.5f,  0.5f, -0.5f,  0,0,1,1 },
    { -0.5f, -0.5f, -0.5f,  1,1,0,1 }, {  0.5f, -0.5f, -0.5f,  1,1,0,1 },
    {  0.5f, -0.5f,  0.5f,  1,1,0,1 }, { -0.5f, -0.5f,  0.5f,  1,1,0,1 },
    { -0.5f,  0.5f,  0.5f,  0,1,1,1 }, { -0.5f,  0.5f, -0.5f,  0,1,1,1 },
    { -0.5f, -0.5f, -0.5f,  0,1,1,1 }, { -0.5f, -0.5f,  0.5f,  0,1,1,1 },
    {  0.5f,  0.5f, -0.5f,  1,0,1,1 }, {  0.5f,  0.5f,  0.5f,  1,0,1,1 },
    {  0.5f, -0.5f,  0.5f,  1,0,1,1 }, {  0.5f, -0.5f, -0.5f,  1,0,1,1 },
};

static unsigned short CubeIndices[] =
{
     0, 1, 2,   0, 2, 3,
     4, 5, 6,   4, 6, 7,
     8, 9,10,   8,10,11,
    12,13,14,  12,14,15,
    16,17,18,  16,18,19,
    20,21,22,  20,22,23,
};

static GhostVertex TriangleVertices[] =
{
    {  0.0f,  1.0f,  0.0f,   1,1,1,1 },
    { -0.5f,  0.0f,  0.0f,   1,0,1,1 },
    {  0.5f,  0.0f,  0.0f,   0,1,1,1 },
};
static unsigned short TriangleIndices[] = { 0, 1, 2 };

static GhostVertex Triangle2Vertices[] =
{
    {  0.0f,  1.0f,  0.0f,   1,1,0,1 },
    { -0.5f,  0.0f,  0.0f,   0,1,0,1 },
    {  0.5f,  0.0f,  0.0f,   1,0,0,1 },
};
static unsigned short Triangle2Indices[] = { 0, 1, 2 };

static GhostVertex ShipVertices[] =
{
    {  3.0f,  0.0f,  0.0f,   0.0f, 1.0f, 0.0f, 1.0f },
    {  0.0f,  3.0f, -3.0f,   0.0f, 0.0f, 1.0f, 1.0f },
    {  0.0f,  0.0f, 10.0f,   1.0f, 0.0f, 0.0f, 1.0f },
    { -3.0f,  0.0f,  0.0f,   0.0f, 1.0f, 1.0f, 1.0f },
    {  3.2f, -1.0f, -3.0f,   0.0f, 0.0f, 1.0f, 1.0f },
    {  3.2f, -1.0f, 11.0f,   0.0f, 1.0f, 0.0f, 1.0f },
    {  2.0f,  1.0f,  2.0f,   1.0f, 0.0f, 0.0f, 1.0f },
    { -3.2f, -1.0f, -3.0f,   0.0f, 0.0f, 1.0f, 1.0f },
    { -3.2f, -1.0f, 11.0f,   0.0f, 1.0f, 0.0f, 1.0f },
    { -2.0f,  1.0f,  2.0f,   1.0f, 0.0f, 0.0f, 1.0f },
};
static unsigned short ShipIndices[] =
{
    0, 1, 2,
    2, 1, 3,
    3, 1, 0,
    0, 2, 3,
    4, 5, 6,
    7, 8, 9,
};

// ============================================================
//  Internal helpers  (no RHI dependency — take raw device)
// ============================================================

static GhostResult CreateDepthBuffer(ID3D12Device* device,
    unsigned int width, unsigned int height,
    GhostPipeline* p)
{
    p->depthBuffer.Reset();

    D3D12_HEAP_PROPERTIES depthHeapProps = {};
    depthHeapProps.Type = D3D12_HEAP_TYPE_DEFAULT;

    D3D12_RESOURCE_DESC depthDesc = {};
    depthDesc.Dimension = D3D12_RESOURCE_DIMENSION_TEXTURE2D;
    depthDesc.Width = width;
    depthDesc.Height = height;
    depthDesc.DepthOrArraySize = 1;
    depthDesc.MipLevels = 1;
    depthDesc.Format = DXGI_FORMAT_D24_UNORM_S8_UINT;
    depthDesc.SampleDesc.Count = 1;
    depthDesc.Flags = D3D12_RESOURCE_FLAG_ALLOW_DEPTH_STENCIL;

    D3D12_CLEAR_VALUE depthClear = {};
    depthClear.Format = DXGI_FORMAT_D24_UNORM_S8_UINT;
    depthClear.DepthStencil.Depth = 1.0f;
    depthClear.DepthStencil.Stencil = 0;

    HRESULT hr = device->CreateCommittedResource(
        &depthHeapProps, D3D12_HEAP_FLAG_NONE, &depthDesc,
        D3D12_RESOURCE_STATE_DEPTH_WRITE, &depthClear,
        IID_PPV_ARGS(&p->depthBuffer));

    if (FAILED(hr)) return GHOST_FAIL;

    D3D12_DEPTH_STENCIL_VIEW_DESC dsvDesc = {};
    dsvDesc.Format = DXGI_FORMAT_D24_UNORM_S8_UINT;
    dsvDesc.ViewDimension = D3D12_DSV_DIMENSION_TEXTURE2D;
    device->CreateDepthStencilView(
        p->depthBuffer.Get(), &dsvDesc,
        p->dsvHeap->GetCPUDescriptorHandleForHeapStart());

    return GHOST_OK;
}

static GhostResult CompileShaderFromFile(const wchar_t* path,
    const char* entry,
    const char* target,
    ID3DBlob** out)
{
    ComPtr<ID3DBlob> err;
    HRESULT hr = D3DCompileFromFile(
        path, nullptr, nullptr, entry, target,
        D3DCOMPILE_DEBUG | D3DCOMPILE_SKIP_OPTIMIZATION, 0, out, &err);
    if (FAILED(hr))
    {
        if (err) OutputDebugStringA((char*)err->GetBufferPointer());
        return GHOST_FAIL;
    }
    return GHOST_OK;
}

static GhostResult CreateUploadBuffer(ID3D12Device* device,
    unsigned long long size,
    ID3D12Resource** out)
{
    D3D12_HEAP_PROPERTIES hp = {};
    hp.Type = D3D12_HEAP_TYPE_UPLOAD;

    D3D12_RESOURCE_DESC rd = {};
    rd.Dimension = D3D12_RESOURCE_DIMENSION_BUFFER;
    rd.Width = size;
    rd.Height = 1;
    rd.DepthOrArraySize = 1;
    rd.MipLevels = 1;
    rd.Format = DXGI_FORMAT_UNKNOWN;
    rd.SampleDesc.Count = 1;
    rd.Layout = D3D12_TEXTURE_LAYOUT_ROW_MAJOR;

    HRESULT hr = device->CreateCommittedResource(
        &hp, D3D12_HEAP_FLAG_NONE, &rd,
        D3D12_RESOURCE_STATE_GENERIC_READ, nullptr, IID_PPV_ARGS(out));
    return FAILED(hr) ? GHOST_FAIL : GHOST_OK;
}

// ============================================================
//  GhostPipelineCreate
// ============================================================

GhostResult GhostPipelineCreate(IGhostRHI* rhi,
    const GhostPipelineDesc* desc,
    GhostPipeline** outPipeline)
{
    GhostPipeline* p = new GhostPipeline();
    ID3D12Device* device = rhi->GetDevice();

    p->viewport = { 0, 0, (float)desc->width, (float)desc->height, 0.0f, 1.0f };
    p->scissorRect = { 0, 0, (long)desc->width,  (long)desc->height };

    // ---- Root Signature ----
    D3D12_ROOT_PARAMETER rp = {};
    rp.ParameterType = D3D12_ROOT_PARAMETER_TYPE_CBV;
    rp.Descriptor.ShaderRegister = 0;
    rp.Descriptor.RegisterSpace = 0;
    rp.ShaderVisibility = D3D12_SHADER_VISIBILITY_VERTEX;

    D3D12_ROOT_SIGNATURE_DESC rsd = {};
    rsd.NumParameters = 1;
    rsd.pParameters = &rp;
    rsd.Flags = D3D12_ROOT_SIGNATURE_FLAG_ALLOW_INPUT_ASSEMBLER_INPUT_LAYOUT;

    ComPtr<ID3DBlob> rsBlob, rsErr;
    if (FAILED(D3D12SerializeRootSignature(&rsd, D3D_ROOT_SIGNATURE_VERSION_1,
        &rsBlob, &rsErr)))
    {
        delete p; return GHOST_FAIL;
    }

    if (FAILED(device->CreateRootSignature(0, rsBlob->GetBufferPointer(),
        rsBlob->GetBufferSize(),
        IID_PPV_ARGS(&p->rootSignature))))
    {
        delete p; return GHOST_FAIL;
    }

    // ---- Shaders ----
    ComPtr<ID3DBlob> vs, ps;
    if (CompileShaderFromFile(L"shaders/GhostVS.hlsl", "VSMain", "vs_5_0", &vs) != GHOST_OK)
    {
        delete p; return GHOST_FAIL;
    }
    if (CompileShaderFromFile(L"shaders/GhostPS.hlsl", "PSMain", "ps_5_0", &ps) != GHOST_OK)
    {
        delete p; return GHOST_FAIL;
    }

    // ---- PSO ----
    D3D12_INPUT_ELEMENT_DESC layout[] =
    {
        { "POSITION", 0, DXGI_FORMAT_R32G32B32_FLOAT,    0,  0, D3D12_INPUT_CLASSIFICATION_PER_VERTEX_DATA, 0 },
        { "COLOR",    0, DXGI_FORMAT_R32G32B32A32_FLOAT, 0, 12, D3D12_INPUT_CLASSIFICATION_PER_VERTEX_DATA, 0 },
    };

    D3D12_GRAPHICS_PIPELINE_STATE_DESC psoDesc = {};
    psoDesc.pRootSignature = p->rootSignature.Get();
    psoDesc.VS = { vs->GetBufferPointer(), vs->GetBufferSize() };
    psoDesc.PS = { ps->GetBufferPointer(), ps->GetBufferSize() };
    psoDesc.InputLayout = { layout, 2 };
    psoDesc.PrimitiveTopologyType = D3D12_PRIMITIVE_TOPOLOGY_TYPE_TRIANGLE;
    psoDesc.RTVFormats[0] = DXGI_FORMAT_R8G8B8A8_UNORM;
    psoDesc.NumRenderTargets = 1;
    psoDesc.SampleDesc.Count = 1;
    psoDesc.SampleMask = UINT_MAX;
    psoDesc.RasterizerState.FillMode = D3D12_FILL_MODE_SOLID;
    psoDesc.RasterizerState.CullMode = D3D12_CULL_MODE_NONE;
    psoDesc.RasterizerState.FrontCounterClockwise = FALSE;
    psoDesc.RasterizerState.DepthClipEnable = TRUE;
    psoDesc.BlendState.RenderTarget[0].RenderTargetWriteMask = D3D12_COLOR_WRITE_ENABLE_ALL;
    psoDesc.DepthStencilState.DepthEnable = TRUE;
    psoDesc.DepthStencilState.DepthWriteMask = D3D12_DEPTH_WRITE_MASK_ALL;
    psoDesc.DepthStencilState.DepthFunc = D3D12_COMPARISON_FUNC_LESS;
    psoDesc.DepthStencilState.StencilEnable = FALSE;
    psoDesc.DSVFormat = DXGI_FORMAT_D24_UNORM_S8_UINT;

    if (FAILED(device->CreateGraphicsPipelineState(&psoDesc, IID_PPV_ARGS(&p->pso))))
    {
        delete p; return GHOST_FAIL;
    }

    // ---- Constant Buffer ----
    const unsigned long long cbSizeSingle = (sizeof(GhostMVP) + 255) & ~255ULL;
    const unsigned long long cbSizeTotal = cbSizeSingle * 4;

    p->cbOffsetCube = 0;
    p->cbOffsetTriangle = cbSizeSingle;
    p->cbOffsetTriangle2 = cbSizeSingle * 2;
    p->cbOffsetShip = cbSizeSingle * 3;

    if (CreateUploadBuffer(device, cbSizeTotal, &p->constantBuffer) != GHOST_OK)
    {
        delete p; return GHOST_FAIL;
    }

    D3D12_RANGE rr = { 0, 0 };
    p->constantBuffer->Map(0, &rr, &p->cbvMappedData);

    // ---- CBV Heap ----
    D3D12_DESCRIPTOR_HEAP_DESC hd = {};
    hd.NumDescriptors = 1;
    hd.Type = D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV;
    hd.Flags = D3D12_DESCRIPTOR_HEAP_FLAG_SHADER_VISIBLE;
    device->CreateDescriptorHeap(&hd, IID_PPV_ARGS(&p->cbvHeap));

    // ---- DSV Heap + Depth Buffer ----
    D3D12_DESCRIPTOR_HEAP_DESC dsvHeapDesc = {};
    dsvHeapDesc.NumDescriptors = 1;
    dsvHeapDesc.Type = D3D12_DESCRIPTOR_HEAP_TYPE_DSV;
    dsvHeapDesc.Flags = D3D12_DESCRIPTOR_HEAP_FLAG_NONE;
    device->CreateDescriptorHeap(&dsvHeapDesc, IID_PPV_ARGS(&p->dsvHeap));

    CreateDepthBuffer(device, desc->width, desc->height, p);

    *outPipeline = p;
    return GHOST_OK;
}

// ============================================================
//  MVP updates  (pipeline-only, no RHI)
// ============================================================

void GhostUpdateMVP(GhostPipeline* pipeline, const GhostMVP* mvp)
{
    memcpy(pipeline->cbvMappedData, mvp, sizeof(GhostMVP));
}
void GhostUpdateCubeMVP(GhostPipeline* pipeline, const GhostMVP* mvp)
{
    memcpy((char*)pipeline->cbvMappedData + pipeline->cbOffsetCube, mvp, sizeof(GhostMVP));
}
void GhostUpdateTriangleMVP(GhostPipeline* pipeline, const GhostMVP* mvp)
{
    memcpy((char*)pipeline->cbvMappedData + pipeline->cbOffsetTriangle, mvp, sizeof(GhostMVP));
}
void GhostUpdateTriangle2MVP(GhostPipeline* pipeline, const GhostMVP* mvp)
{
    memcpy((char*)pipeline->cbvMappedData + pipeline->cbOffsetTriangle2, mvp, sizeof(GhostMVP));
}
void GhostUpdateShipMVP(GhostPipeline* pipeline, const GhostMVP* mvp)
{
    memcpy((char*)pipeline->cbvMappedData + pipeline->cbOffsetShip, mvp, sizeof(GhostMVP));
}

// ============================================================
//  Geometry upload
// ============================================================

GhostResult GhostUploadCubeGeometry(IGhostRHI* rhi, GhostPipeline* pipeline)
{
    ID3D12Device* device = rhi->GetDevice();
    D3D12_RANGE rr = { 0, 0 };

    const unsigned long long vbSize = sizeof(CubeVertices);
    if (CreateUploadBuffer(device, vbSize, &pipeline->vertexBuffer) != GHOST_OK) return GHOST_FAIL;
    void* vb = nullptr;
    pipeline->vertexBuffer->Map(0, &rr, &vb);
    memcpy(vb, CubeVertices, vbSize);
    pipeline->vertexBuffer->Unmap(0, nullptr);
    pipeline->vertexBufferView.BufferLocation = pipeline->vertexBuffer->GetGPUVirtualAddress();
    pipeline->vertexBufferView.SizeInBytes = (unsigned int)vbSize;
    pipeline->vertexBufferView.StrideInBytes = sizeof(GhostVertex);

    const unsigned long long ibSize = sizeof(CubeIndices);
    if (CreateUploadBuffer(device, ibSize, &pipeline->indexBuffer) != GHOST_OK) return GHOST_FAIL;
    void* ib = nullptr;
    pipeline->indexBuffer->Map(0, &rr, &ib);
    memcpy(ib, CubeIndices, ibSize);
    pipeline->indexBuffer->Unmap(0, nullptr);
    pipeline->indexBufferView.BufferLocation = pipeline->indexBuffer->GetGPUVirtualAddress();
    pipeline->indexBufferView.SizeInBytes = (unsigned int)ibSize;
    pipeline->indexBufferView.Format = DXGI_FORMAT_R16_UINT;
    pipeline->indexCount = sizeof(CubeIndices) / sizeof(CubeIndices[0]);

    return GHOST_OK;
}

GhostResult GhostUploadTriangleGeometry(IGhostRHI* rhi, GhostPipeline* pipeline)
{
    ID3D12Device* device = rhi->GetDevice();
    D3D12_RANGE rr = { 0, 0 };

    const unsigned long long vbSize = sizeof(TriangleVertices);
    ComPtr<ID3D12Resource> triangleVB;
    if (CreateUploadBuffer(device, vbSize, &triangleVB) != GHOST_OK) return GHOST_FAIL;
    void* vb = nullptr;
    triangleVB->Map(0, &rr, &vb);
    memcpy(vb, TriangleVertices, vbSize);
    triangleVB->Unmap(0, nullptr);
    pipeline->triangleVB = triangleVB;
    pipeline->triangleVBView.BufferLocation = triangleVB->GetGPUVirtualAddress();
    pipeline->triangleVBView.SizeInBytes = (unsigned int)vbSize;
    pipeline->triangleVBView.StrideInBytes = sizeof(GhostVertex);

    const unsigned long long ibSize = sizeof(TriangleIndices);
    ComPtr<ID3D12Resource> triangleIB;
    if (CreateUploadBuffer(device, ibSize, &triangleIB) != GHOST_OK) return GHOST_FAIL;
    void* ib = nullptr;
    triangleIB->Map(0, &rr, &ib);
    memcpy(ib, TriangleIndices, ibSize);
    triangleIB->Unmap(0, nullptr);
    pipeline->triangleIB = triangleIB;
    pipeline->triangleIBView.BufferLocation = triangleIB->GetGPUVirtualAddress();
    pipeline->triangleIBView.SizeInBytes = (unsigned int)ibSize;
    pipeline->triangleIBView.Format = DXGI_FORMAT_R16_UINT;
    pipeline->triangleIndexCount = sizeof(TriangleIndices) / sizeof(TriangleIndices[0]);

    return GHOST_OK;
}

GhostResult GhostUploadTriangle2Geometry(IGhostRHI* rhi, GhostPipeline* pipeline)
{
    ID3D12Device* device = rhi->GetDevice();
    D3D12_RANGE rr = { 0, 0 };

    const unsigned long long vbSize = sizeof(Triangle2Vertices);
    ComPtr<ID3D12Resource> tri2VB;
    if (CreateUploadBuffer(device, vbSize, &tri2VB) != GHOST_OK) return GHOST_FAIL;
    void* vb = nullptr;
    tri2VB->Map(0, &rr, &vb);
    memcpy(vb, Triangle2Vertices, vbSize);
    tri2VB->Unmap(0, nullptr);
    pipeline->triangle2VB = tri2VB;
    pipeline->triangle2VBView.BufferLocation = tri2VB->GetGPUVirtualAddress();
    pipeline->triangle2VBView.SizeInBytes = (unsigned int)vbSize;
    pipeline->triangle2VBView.StrideInBytes = sizeof(GhostVertex);

    const unsigned long long ibSize = sizeof(Triangle2Indices);
    ComPtr<ID3D12Resource> tri2IB;
    if (CreateUploadBuffer(device, ibSize, &tri2IB) != GHOST_OK) return GHOST_FAIL;
    void* ib = nullptr;
    tri2IB->Map(0, &rr, &ib);
    memcpy(ib, Triangle2Indices, ibSize);
    tri2IB->Unmap(0, nullptr);
    pipeline->triangle2IB = tri2IB;
    pipeline->triangle2IBView.BufferLocation = tri2IB->GetGPUVirtualAddress();
    pipeline->triangle2IBView.SizeInBytes = (unsigned int)ibSize;
    pipeline->triangle2IBView.Format = DXGI_FORMAT_R16_UINT;
    pipeline->triangle2IndexCount = sizeof(Triangle2Indices) / sizeof(Triangle2Indices[0]);

    return GHOST_OK;
}

GhostResult GhostUploadShipGeometry(IGhostRHI* rhi, GhostPipeline* pipeline)
{
    ID3D12Device* device = rhi->GetDevice();
    D3D12_RANGE rr = { 0, 0 };

    const unsigned long long vbSize = sizeof(ShipVertices);
    ComPtr<ID3D12Resource> shipVB;
    if (CreateUploadBuffer(device, vbSize, &shipVB) != GHOST_OK) return GHOST_FAIL;
    void* vb = nullptr;
    shipVB->Map(0, &rr, &vb);
    memcpy(vb, ShipVertices, vbSize);
    shipVB->Unmap(0, nullptr);
    pipeline->shipVB = shipVB;
    pipeline->shipVBView.BufferLocation = shipVB->GetGPUVirtualAddress();
    pipeline->shipVBView.SizeInBytes = (unsigned int)vbSize;
    pipeline->shipVBView.StrideInBytes = sizeof(GhostVertex);

    const unsigned long long ibSize = sizeof(ShipIndices);
    ComPtr<ID3D12Resource> shipIB;
    if (CreateUploadBuffer(device, ibSize, &shipIB) != GHOST_OK) return GHOST_FAIL;
    void* ib = nullptr;
    shipIB->Map(0, &rr, &ib);
    memcpy(ib, ShipIndices, ibSize);
    shipIB->Unmap(0, nullptr);
    pipeline->shipIB = shipIB;
    pipeline->shipIBView.BufferLocation = shipIB->GetGPUVirtualAddress();
    pipeline->shipIBView.SizeInBytes = (unsigned int)ibSize;
    pipeline->shipIBView.Format = DXGI_FORMAT_R16_UINT;
    pipeline->shipIndexCount = sizeof(ShipIndices) / sizeof(ShipIndices[0]);

    return GHOST_OK;
}

// ============================================================
//  Draw calls
// ============================================================

void GhostDrawCube(IGhostRHI* rhi, GhostPipeline* pipeline)
{
    auto* cmd = rhi->GetCmdList();

    ID3D12DescriptorHeap* heaps[] = { pipeline->cbvHeap.Get() };
    cmd->SetDescriptorHeaps(1, heaps);
    cmd->SetPipelineState(pipeline->pso.Get());
    cmd->SetGraphicsRootSignature(pipeline->rootSignature.Get());
    cmd->SetGraphicsRootConstantBufferView(
        0, pipeline->constantBuffer->GetGPUVirtualAddress() + pipeline->cbOffsetCube);

    D3D12_CPU_DESCRIPTOR_HANDLE rtv = rhi->GetRTV();
    D3D12_CPU_DESCRIPTOR_HANDLE dsv = pipeline->dsvHeap->GetCPUDescriptorHandleForHeapStart();
    cmd->OMSetRenderTargets(1, &rtv, FALSE, &dsv);

    cmd->IASetPrimitiveTopology(D3D_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
    cmd->IASetVertexBuffers(0, 1, &pipeline->vertexBufferView);
    cmd->IASetIndexBuffer(&pipeline->indexBufferView);
    cmd->DrawIndexedInstanced(pipeline->indexCount, 1, 0, 0, 0);
}

void GhostDrawTriangle(IGhostRHI* rhi, GhostPipeline* pipeline)
{
    auto* cmd = rhi->GetCmdList();

    ID3D12DescriptorHeap* heaps[] = { pipeline->cbvHeap.Get() };
    cmd->SetDescriptorHeaps(1, heaps);
    cmd->SetPipelineState(pipeline->pso.Get());
    cmd->SetGraphicsRootSignature(pipeline->rootSignature.Get());
    cmd->SetGraphicsRootConstantBufferView(
        0, pipeline->constantBuffer->GetGPUVirtualAddress() + pipeline->cbOffsetTriangle);

    D3D12_CPU_DESCRIPTOR_HANDLE rtv = rhi->GetRTV();
    D3D12_CPU_DESCRIPTOR_HANDLE dsv = pipeline->dsvHeap->GetCPUDescriptorHandleForHeapStart();
    cmd->OMSetRenderTargets(1, &rtv, FALSE, &dsv);

    cmd->IASetPrimitiveTopology(D3D_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
    cmd->IASetVertexBuffers(0, 1, &pipeline->triangleVBView);
    cmd->IASetIndexBuffer(&pipeline->triangleIBView);
    cmd->DrawIndexedInstanced(pipeline->triangleIndexCount, 1, 0, 0, 0);
}

void GhostDrawTriangle2(IGhostRHI* rhi, GhostPipeline* pipeline)
{
    auto* cmd = rhi->GetCmdList();

    ID3D12DescriptorHeap* heaps[] = { pipeline->cbvHeap.Get() };
    cmd->SetDescriptorHeaps(1, heaps);
    cmd->SetPipelineState(pipeline->pso.Get());
    cmd->SetGraphicsRootSignature(pipeline->rootSignature.Get());
    cmd->SetGraphicsRootConstantBufferView(
        0, pipeline->constantBuffer->GetGPUVirtualAddress() + pipeline->cbOffsetTriangle2);

    D3D12_CPU_DESCRIPTOR_HANDLE rtv = rhi->GetRTV();
    D3D12_CPU_DESCRIPTOR_HANDLE dsv = pipeline->dsvHeap->GetCPUDescriptorHandleForHeapStart();
    cmd->OMSetRenderTargets(1, &rtv, FALSE, &dsv);

    cmd->IASetPrimitiveTopology(D3D_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
    cmd->IASetVertexBuffers(0, 1, &pipeline->triangle2VBView);
    cmd->IASetIndexBuffer(&pipeline->triangle2IBView);
    cmd->DrawIndexedInstanced(pipeline->triangle2IndexCount, 1, 0, 0, 0);
}

void GhostDrawShip(IGhostRHI* rhi, GhostPipeline* pipeline)
{
    auto* cmd = rhi->GetCmdList();

    ID3D12DescriptorHeap* heaps[] = { pipeline->cbvHeap.Get() };
    cmd->SetDescriptorHeaps(1, heaps);
    cmd->SetPipelineState(pipeline->pso.Get());
    cmd->SetGraphicsRootSignature(pipeline->rootSignature.Get());
    cmd->SetGraphicsRootConstantBufferView(
        0, pipeline->constantBuffer->GetGPUVirtualAddress() + pipeline->cbOffsetShip);

    cmd->IASetPrimitiveTopology(D3D_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
    cmd->IASetVertexBuffers(0, 1, &pipeline->shipVBView);
    cmd->IASetIndexBuffer(&pipeline->shipIBView);
    cmd->DrawIndexedInstanced(pipeline->shipIndexCount, 1, 0, 0, 0);
}

// ============================================================
//  Resize + Destroy
// ============================================================

GhostResult GhostResizeDepthBuffer(IGhostRHI* rhi, GhostPipeline* pipeline,
    unsigned int width, unsigned int height)
{
    return CreateDepthBuffer(rhi->GetDevice(), width, height, pipeline);
}

void GhostPipelineDestroy(GhostPipeline* pipeline)
{
    if (!pipeline) return;
    if (pipeline->cbvMappedData)
        pipeline->constantBuffer->Unmap(0, nullptr);
    delete pipeline;
}