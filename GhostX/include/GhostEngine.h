// GhostEngine.h
#pragma once
#include <Windows.h>

#ifdef GHOSTENGINE_EXPORTS
#define GHOST_API __declspec(dllexport)
#else
#define GHOST_API __declspec(dllimport)
#endif

extern "C"
{
    GHOST_API int  GhostEngine_Init(HWND hwnd, unsigned int width, unsigned int height);
    GHOST_API void GhostEngine_SetClearColor(float r, float g, float b);
    GHOST_API void GhostEngine_Tick();
    GHOST_API void GhostEngine_Resize(unsigned int width, unsigned int height);
    GHOST_API void GhostEngine_Shutdown();
}