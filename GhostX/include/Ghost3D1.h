// Ghost3D1.h
#pragma once
#include <Windows.h>

typedef long GhostResult;
constexpr long GHOST_OK = 0L;
constexpr long GHOST_FAIL = -1L;

struct GhostColor { float r, g, b, a; };
struct GhostViewport { float x, y, width, height, minDepth, maxDepth; };
struct GhostWindowDesc { const wchar_t* title; unsigned int width; unsigned int height; };
struct GhostContext;
struct GhostPipeline;
struct ID3D12Device;
struct ID3D12GraphicsCommandList;
struct D3D12_CPU_DESCRIPTOR_HANDLE;

GhostResult GhostBoot(HWND hwnd, unsigned int width, unsigned int height, GhostContext** outCtx);
void        GhostShutdown(GhostContext* ctx);
void        GhostPresent(GhostContext* ctx);
void        GhostClear(GhostContext* ctx, const GhostColor* color, GhostPipeline* pipeline);
void        GhostSetClearColor(GhostContext* ctx, float r, float g, float b);

ID3D12Device* GhostGetDevice(GhostContext* ctx);
ID3D12GraphicsCommandList* GhostGetCmdList(GhostContext* ctx);
D3D12_CPU_DESCRIPTOR_HANDLE GhostGetRTV(GhostContext* ctx);
