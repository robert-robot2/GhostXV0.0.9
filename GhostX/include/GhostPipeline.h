// GhostPipeline.h
#pragma once
#include "../include/GhostMath.h"
#include "../include/Ghost3D1.h"
#include "../include/IGhostRHI.h"
#include <d3d12.h>
#include <d3dcompiler.h>
#include <wrl/client.h>
using Microsoft::WRL::ComPtr;

struct GhostVertex
{
    float x, y, z;
    float r, g, b, a;
};

struct GhostPipeline
{
    ComPtr<ID3D12RootSignature>  rootSignature;
    ComPtr<ID3D12PipelineState>  pso;

    // ---- Cube Geometry ----
    ComPtr<ID3D12Resource>       vertexBuffer;
    D3D12_VERTEX_BUFFER_VIEW     vertexBufferView = {};
    ComPtr<ID3D12Resource>       indexBuffer;
    D3D12_INDEX_BUFFER_VIEW      indexBufferView = {};
    unsigned int                 indexCount = 0;

    // ---- Triangle Geometry ----
    ComPtr<ID3D12Resource>       triangleVB;
    D3D12_VERTEX_BUFFER_VIEW     triangleVBView = {};
    ComPtr<ID3D12Resource>       triangleIB;
    D3D12_INDEX_BUFFER_VIEW      triangleIBView = {};
    unsigned int                 triangleIndexCount = 0;

    // ---- Triangle 2 Geometry ----
    ComPtr<ID3D12Resource>       triangle2VB;
    D3D12_VERTEX_BUFFER_VIEW     triangle2VBView = {};
    ComPtr<ID3D12Resource>       triangle2IB;
    D3D12_INDEX_BUFFER_VIEW      triangle2IBView = {};
    unsigned int                 triangle2IndexCount = 0;

    // ---- Ship Geometry ----
    ComPtr<ID3D12Resource>       shipVB;
    D3D12_VERTEX_BUFFER_VIEW     shipVBView = {};
    ComPtr<ID3D12Resource>       shipIB;
    D3D12_INDEX_BUFFER_VIEW      shipIBView = {};
    unsigned int                 shipIndexCount = 0;

    // ---- Constant Buffer ----
    ComPtr<ID3D12Resource>       constantBuffer;
    ComPtr<ID3D12DescriptorHeap> cbvHeap;
    void* cbvMappedData = nullptr;

    // ---- Depth Buffer ----
    ComPtr<ID3D12Resource>       depthBuffer;
    ComPtr<ID3D12DescriptorHeap> dsvHeap;

    // ---- CB offsets (bytes) ----
    unsigned long long cbOffsetCube = 0;
    unsigned long long cbOffsetTriangle = 0;
    unsigned long long cbOffsetTriangle2 = 0;
    unsigned long long cbOffsetShip = 0;

    D3D12_VIEWPORT viewport = {};
    D3D12_RECT     scissorRect = {};
};

struct GhostPipelineDesc { unsigned int width = 1280; unsigned int height = 720; };

// ---- Lifecycle ----
GhostResult GhostPipelineCreate(IGhostRHI* rhi, const GhostPipelineDesc* desc, GhostPipeline** outPipeline);
void        GhostPipelineDestroy(GhostPipeline* pipeline);

// ---- Geometry upload ----
GhostResult GhostUploadCubeGeometry(IGhostRHI* rhi, GhostPipeline* pipeline);
GhostResult GhostUploadTriangleGeometry(IGhostRHI* rhi, GhostPipeline* pipeline);
GhostResult GhostUploadTriangle2Geometry(IGhostRHI* rhi, GhostPipeline* pipeline);
GhostResult GhostUploadShipGeometry(IGhostRHI* rhi, GhostPipeline* pipeline);

// ---- MVP updates (pipeline-only, no RHI needed) ----
void GhostUpdateMVP(GhostPipeline* pipeline, const GhostMVP* mvp);
void GhostUpdateCubeMVP(GhostPipeline* pipeline, const GhostMVP* mvp);
void GhostUpdateTriangleMVP(GhostPipeline* pipeline, const GhostMVP* mvp);
void GhostUpdateTriangle2MVP(GhostPipeline* pipeline, const GhostMVP* mvp);
void GhostUpdateShipMVP(GhostPipeline* pipeline, const GhostMVP* mvp);

// ---- Draw calls ----
void GhostDrawCube(IGhostRHI* rhi, GhostPipeline* pipeline);
void GhostDrawTriangle(IGhostRHI* rhi, GhostPipeline* pipeline);
void GhostDrawTriangle2(IGhostRHI* rhi, GhostPipeline* pipeline);
void GhostDrawShip(IGhostRHI* rhi, GhostPipeline* pipeline);

// ---- Resize ----
GhostResult GhostResizeDepthBuffer(IGhostRHI* rhi, GhostPipeline* pipeline,
    unsigned int width, unsigned int height);