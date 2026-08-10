// GhostEngine.cpp  — updated for IGhostRHI
#include "../include/GhostEngine.h"
#include "../include/IGhostRHI.h"
#include "../include/Ghost3D1.h"
#include "../include/GhostMath.h"
#include "../include/GhostPipeline.h"

// ---- Engine state ----
static IGhostRHI* g_rhi = nullptr;
static GhostPipeline* g_pipeline = nullptr;

// ---- Camera state ----
static GhostVec3 g_eye = { 0.0f, 0.75f, -2.0f };
static float     g_yaw = 0.0f;
static float     g_pitch = 0.0f;
static bool      g_looking = false;
static POINT     g_lastMouse = {};

// ---- Timing ----
static LARGE_INTEGER g_freq = {};
static LARGE_INTEGER g_prev = {};

// ---- Scene angles ----
static float g_triAngle = 0.0f;
static float g_tri2Angle = 3.1415926535f;
static float g_shipAngle = 0.0f;
static GhostColor g_clearColor = { 0.0f, 0.0f, 0.0f, 1.0f };

void GhostEngine_SetClearColor(float r, float g, float b)
{
    g_clearColor = { r, g, b, 1.0f };
    if (g_rhi) g_rhi->SetClearColor(r, g, b);
}

int GhostEngine_Init(HWND hwnd, unsigned int width, unsigned int height)
{
    g_rhi = CreateRHI(RHIType::DX12);
    if (!g_rhi) return -1;
    if (g_rhi->Init(hwnd, width, height) != GHOST_OK) return -1;

    GhostPipelineDesc pd = {};
    pd.width = width;
    pd.height = height;

    // Pipeline still takes device/cmdlist via the RHI accessors —
    // these are thin wrappers, no D3D12 types leak into Engine.
    if (GhostPipelineCreate(g_rhi, &pd, &g_pipeline) != GHOST_OK) return -1;
    if (GhostUploadCubeGeometry(g_rhi, g_pipeline) != GHOST_OK) return -1;
    if (GhostUploadTriangleGeometry(g_rhi, g_pipeline) != GHOST_OK) return -1;
    if (GhostUploadTriangle2Geometry(g_rhi, g_pipeline) != GHOST_OK) return -1;
    if (GhostUploadShipGeometry(g_rhi, g_pipeline) != GHOST_OK) return -1;

    QueryPerformanceFrequency(&g_freq);
    QueryPerformanceCounter(&g_prev);

    return 0;
}

void GhostEngine_Tick()
{
    if (!g_rhi || !g_pipeline) return;

    // ---- Delta time ----
    LARGE_INTEGER curr;
    QueryPerformanceCounter(&curr);
    float dt = (float)(curr.QuadPart - g_prev.QuadPart) / (float)g_freq.QuadPart;
    g_prev = curr;

    // ---- Mouse look ----
    const float lookSens = 0.0025f;
    const float moveSpeed = 3.5f;

    if (GetAsyncKeyState(VK_RBUTTON) & 0x8000)
    {
        if (!g_looking)
        {
            g_looking = true;
            GetCursorPos(&g_lastMouse);
            ShowCursor(FALSE);
        }
        else
        {
            POINT cur;
            GetCursorPos(&cur);
            int dx = cur.x - g_lastMouse.x;
            int dy = cur.y - g_lastMouse.y;
            g_yaw -= dx * lookSens;
            g_pitch -= dy * lookSens;
            const float limit = 3.1415926535f * 0.49f;
            if (g_pitch > limit) g_pitch = limit;
            if (g_pitch < -limit) g_pitch = -limit;
            SetCursorPos(g_lastMouse.x, g_lastMouse.y);
        }
    }
    else if (g_looking)
    {
        g_looking = false;
        ShowCursor(TRUE);
    }

    // ---- Movement ----
    GhostVec3 forward = {
        cosf(g_pitch) * sinf(g_yaw),
        sinf(g_pitch),
        cosf(g_pitch) * cosf(g_yaw)
    };
    forward = forward.Normalize();
    GhostVec3 worldUp = { 0.0f, 1.0f, 0.0f };
    GhostVec3 right = forward.Cross(worldUp).Normalize();
    float speed = moveSpeed * dt;

    if (GetAsyncKeyState('W') & 0x8000) g_eye = g_eye + forward * speed;
    if (GetAsyncKeyState('S') & 0x8000) g_eye = g_eye - forward * speed;
    if (GetAsyncKeyState('A') & 0x8000) g_eye = g_eye - right * speed;
    if (GetAsyncKeyState('D') & 0x8000) g_eye = g_eye + right * speed;
    if (GetAsyncKeyState(VK_SPACE) & 0x8000) g_eye.y += speed;
    if (GetAsyncKeyState(VK_CONTROL) & 0x8000) g_eye.y -= speed;

    // ---- Matrices ----
    GhostVec3 target = g_eye + forward;
    GhostMat4 view = GhostMat4::LookAtLH(g_eye, target, worldUp);
    float aspect = 1280.0f / 720.0f;
    GhostMat4 proj = GhostMat4::PerspectiveFovLH(
        3.1415926535f / 4.0f, aspect, 0.1f, 100.0f);

    // ---- Begin frame ----
    D3D12_CPU_DESCRIPTOR_HANDLE dsv =
        g_pipeline->dsvHeap->GetCPUDescriptorHandleForHeapStart();
    g_rhi->BeginFrame(&g_clearColor, dsv);

    // ---- Cube ----
    GhostMVP cubeMvp;
    cubeMvp.model = GhostMat4::Identity();
    cubeMvp.view = view;
    cubeMvp.proj = proj;
    GhostUpdateCubeMVP(g_pipeline, &cubeMvp);
    GhostDrawCube(g_rhi, g_pipeline);

    // ---- Triangles ----
    g_triAngle += 1.2f * dt;
    g_tri2Angle += 1.2f * dt;
    g_shipAngle += 0.8f * dt;

    GhostMVP triMvp;
    triMvp.model =
        GhostMat4::Translation(0.0f, 0.75f, 0.0f) *
        GhostMat4::RotationY(g_triAngle) *
        GhostMat4::Translation(1.2f, 0.0f, 0.0f);
    triMvp.view = view;
    triMvp.proj = proj;
    GhostUpdateTriangleMVP(g_pipeline, &triMvp);
    GhostDrawTriangle(g_rhi, g_pipeline);

    GhostMVP tri2Mvp;
    tri2Mvp.model =
        GhostMat4::Translation(0.0f, 0.75f, 0.0f) *
        GhostMat4::RotationY(g_tri2Angle) *
        GhostMat4::Translation(1.2f, 0.0f, 0.0f);
    tri2Mvp.view = view;
    tri2Mvp.proj = proj;
    GhostUpdateTriangle2MVP(g_pipeline, &tri2Mvp);
    GhostDrawTriangle2(g_rhi, g_pipeline);

    // ---- Ship ----
    GhostMVP shipMvp;
    shipMvp.model =
        GhostMat4::Translation(5.0f, 0.75f, 0.0f) *
        GhostMat4::RotationY(g_shipAngle);
    shipMvp.view = view;
    shipMvp.proj = proj;
    GhostUpdateShipMVP(g_pipeline, &shipMvp);
    GhostDrawShip(g_rhi, g_pipeline);

    // ---- End frame ----
    g_rhi->EndFrame();
}

void GhostEngine_Resize(unsigned int width, unsigned int height)
{
    if (!g_rhi || !g_pipeline) return;
    g_rhi->Resize(width, height);
    GhostResizeDepthBuffer(g_rhi, g_pipeline, width, height);
}

void GhostEngine_Shutdown()
{
    GhostPipelineDestroy(g_pipeline);
    g_pipeline = nullptr;

    if (g_rhi)
    {
        g_rhi->Shutdown();
        delete g_rhi;
        g_rhi = nullptr;
    }
}