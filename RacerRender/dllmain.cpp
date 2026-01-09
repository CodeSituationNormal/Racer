#include <windows.h>
#include <gl/gl.h>
#include "stb_image.h"
#include <math.h>
#include <stdio.h>
#include <random>

static HDC   g_hDC = nullptr;
static HGLRC g_hRC = nullptr;
static HWND  g_hWnd = nullptr;
GLuint g_fontBase = 0;

GLuint g_playerTex = 0;
GLuint g_blueEnemyTex = 0;
GLuint g_purpleEnemyTex = 0;
GLuint g_greenEnemyTex = 0;
GLuint g_pinkEnemyTex = 0;
GLuint g_yellowEnemyTex = 0;
GLuint g_roadTex = 0;
GLuint g_grassTex = 0;
GLuint g_dashTex = 0;
GLuint g_coinTex = 0;
GLuint g_explosionTex = 0;

#define ENEMY_BLUE    0
#define ENEMY_YELLOW  1
#define ENEMY_GREEN   2
#define ENEMY_PURPLE  3
#define ENEMY_PINK    4

bool LoadTexturePNG(const char* filename, GLuint* texID)
{
    int w, h, channels;
    stbi_set_flip_vertically_on_load(true);

    unsigned char* data = stbi_load(filename, &w, &h, &channels, 4);
    if (!data)
    {
        const char* reason = stbi_failure_reason();
        if (!reason) reason = "Unknown stb_image error";

        MessageBoxA(
            nullptr,
            reason,
            "stbi_load failed",
            MB_OK | MB_ICONERROR
        );
        return false;
    }

    glGenTextures(1, texID);
    glBindTexture(GL_TEXTURE_2D, *texID);

    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);

    glTexImage2D(
        GL_TEXTURE_2D,
        0,
        GL_RGBA,
        w, h,
        0,
        GL_RGBA,
        GL_UNSIGNED_BYTE,
        data
    );

    GLenum err = glGetError();
    if (err != GL_NO_ERROR)
    {
        char buf[64];
        sprintf_s(buf, "glTexImage2D error: %x", err);
        MessageBoxA(nullptr, buf, "GL ERROR", MB_OK);
    }


    stbi_image_free(data);
    return true;
}

extern "C" __declspec(dllexport)
bool InitOpenGL(HWND hwnd, int width, int height)
{
    g_hWnd = hwnd;
    g_hDC = GetDC(hwnd);
    if (!g_hDC) return false;

    PIXELFORMATDESCRIPTOR pfd = {};
    pfd.nSize = sizeof(pfd);
    pfd.nVersion = 1;
    pfd.dwFlags =
        PFD_DRAW_TO_WINDOW |
        PFD_SUPPORT_OPENGL |
        PFD_DOUBLEBUFFER;
    pfd.iPixelType = PFD_TYPE_RGBA;
    pfd.cColorBits = 32;
    pfd.cDepthBits = 24;
    pfd.iLayerType = PFD_MAIN_PLANE;

    int pf = ChoosePixelFormat(g_hDC, &pfd);
    if (!pf) return false;

    if (!SetPixelFormat(g_hDC, pf, &pfd)) return false;

    g_hRC = wglCreateContext(g_hDC);
    if (!g_hRC) return false;

    if (!wglMakeCurrent(g_hDC, g_hRC)) return false;

    glViewport(0, 0, width, height);

    glMatrixMode(GL_PROJECTION);
    glLoadIdentity();
    glOrtho(-1, 1, -1, 1, -1, 1);

    glMatrixMode(GL_MODELVIEW);
    glLoadIdentity();

    glDisable(GL_DEPTH_TEST);
    glEnable(GL_BLEND);
    glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
    glEnable(GL_TEXTURE_2D);

    OutputDebugStringA("OpenGL context OK\n");
    return true;
}

extern "C" __declspec(dllexport)
bool InitFont()
{
    HFONT font = CreateFont(
        24,                          
        0,                           
        0, 0,                        
        FW_BOLD,                     
        FALSE, FALSE, FALSE,         
        ANSI_CHARSET,                
        OUT_TT_PRECIS,             
        CLIP_DEFAULT_PRECIS,
        ANTIALIASED_QUALITY,        
        FF_DONTCARE | DEFAULT_PITCH,
        L"Arial"                   
    );

    if (!font) return false;

    HDC hdc = g_hDC; 
    SelectObject(hdc, font);

    g_fontBase = glGenLists(96);
    bool success = wglUseFontBitmaps(hdc, 32, 96, g_fontBase);

    DeleteObject(font);

    return success;
}

extern "C" __declspec(dllexport)
bool InitTextures()
{
    bool success = (LoadTexturePNG("car_red.png", &g_playerTex) 
        && LoadTexturePNG("car_blue.png", &g_blueEnemyTex)
        && LoadTexturePNG("car_yellow.png", &g_yellowEnemyTex)
        && LoadTexturePNG("car_green.png", &g_greenEnemyTex)
        && LoadTexturePNG("car_purple.png", &g_purpleEnemyTex)
        && LoadTexturePNG("car_pink.png", &g_pinkEnemyTex)
        && LoadTexturePNG("coin.png", &g_coinTex)
        && LoadTexturePNG("explosion.png", &g_explosionTex)
        && LoadTexturePNG("road.png", &g_roadTex)
        && LoadTexturePNG("dash.png", &g_dashTex)
        && LoadTexturePNG("grass.png", &g_grassTex));
    return success;
}

extern "C" __declspec(dllexport)
void RenderText(float x, float y, const char* text)
{
    if (g_fontBase == 0 || !text) return;

    glPushAttrib(GL_ENABLE_BIT | GL_CURRENT_BIT | GL_LIST_BIT);

    glDisable(GL_TEXTURE_2D);
    glDisable(GL_DEPTH_TEST);
    glEnable(GL_BLEND);
    glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);

    glColor4f(1.0f, 1.0f, 1.0f, 1.0f);

    glRasterPos2f(x, y);

    glListBase(g_fontBase - 32);
    glCallLists((GLsizei)strlen(text), GL_UNSIGNED_BYTE, text);

    glPopAttrib();
}

extern "C" __declspec(dllexport)
void RenderFrame(
    float playerX,
    float offset,
    const float* enemiesX,
    const float* enemiesY,
    const int* enemiesType,
    const int enemyCount,
    float coinX,
    float coinY,
    int score,
    int kills,
    float explosionX,
    float explosionY,
    float gameTime,
    int lives)
{
    if (!g_hDC || !g_hRC) return;
    wglMakeCurrent(g_hDC, g_hRC);

    glClearColor(1.0f, 1.0f, 1.0f, 1.0f);
    glClear(GL_COLOR_BUFFER_BIT);
    glLoadIdentity();

    glBindTexture(GL_TEXTURE_2D, g_grassTex);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_REPEAT);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_REPEAT);
    float tileFactorX = 5.0f; // сколько раз текстура повторяется по X
    float tileFactorY = 5.0f; // сколько раз по Y

    glBegin(GL_QUADS);
    glTexCoord2f(0.0f, 0.0f); glVertex2f(-1.0f, -1.0f);
    glTexCoord2f(tileFactorX, 0.0f); glVertex2f(1.0f, -1.0f);
    glTexCoord2f(tileFactorX, tileFactorY); glVertex2f(1.0f, 1.0f);
    glTexCoord2f(0.0f, tileFactorY); glVertex2f(-1.0f, 1.0f);
    glEnd();

    // ROAD
    glClearColor(1.0f, 1.0f, 1.0f, 1.0f);
    glBindTexture(GL_TEXTURE_2D, g_roadTex);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_REPEAT);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_REPEAT);
    glBegin(GL_QUADS);

    glTexCoord2f(0.0f, 0.0f); glVertex2f(0.6f, -1);
    glTexCoord2f(tileFactorX, 0.0f); glVertex2f(-0.6f, -1);
    glTexCoord2f(tileFactorX, tileFactorY); glVertex2f(-0.6f, 1);
    glTexCoord2f(0.0f, tileFactorY); glVertex2f(0.6f, 1);
    glEnd();

    // DASHES
    glLineWidth(5);
    float dash = 0.2f, gap = 0.2f;
    float period = dash + gap;

    glBindTexture(GL_TEXTURE_2D, g_dashTex);
    glBegin(GL_LINES);
    for (float y = -1.5f; y < 1.5f; y += period)
    {
        float yy = y - fmodf(offset, period);
        glVertex2f(0.015f, yy);
        glVertex2f(0.015f, yy + dash);
    }
    glEnd();

    float carW = 0.16f, carH = 0.2f;

    // PLAYER
    glColor4f(1.0f, 1.0f, 1.0f, 1.0f);
    glBindTexture(GL_TEXTURE_2D, g_playerTex);
    glBegin(GL_QUADS);
    glTexCoord2f(0, 0); glVertex2f(playerX - carW / 2, -0.75f - carH / 2);
    glTexCoord2f(1, 0); glVertex2f(playerX + carW / 2, -0.75f - carH / 2);
    glTexCoord2f(1, 1); glVertex2f(playerX + carW / 2, -0.75f + carH / 2);
    glTexCoord2f(0, 1); glVertex2f(playerX - carW / 2, -0.75f + carH / 2);
    glEnd();

    // ENEMIES
    for (int i = 0; i < enemyCount; i++)
    {
        GLuint tex = g_blueEnemyTex;
        switch (enemiesType[i])
        {
        case 0: tex = g_blueEnemyTex; break;
        case 1: tex = g_yellowEnemyTex; break;
        case 2: tex = g_greenEnemyTex; break;
        case 3: tex = g_purpleEnemyTex; break;
        case 4: tex = g_pinkEnemyTex; break;
        }

        glBindTexture(GL_TEXTURE_2D, tex);
        glBegin(GL_QUADS);
        glTexCoord2f(0, 0); glVertex2f(enemiesX[i] - carW / 2, enemiesY[i] - carH / 2);
        glTexCoord2f(1, 0); glVertex2f(enemiesX[i] + carW / 2, enemiesY[i] - carH / 2);
        glTexCoord2f(1, 1); glVertex2f(enemiesX[i] + carW / 2, enemiesY[i] + carH / 2);
        glTexCoord2f(0, 1); glVertex2f(enemiesX[i] - carW / 2, enemiesY[i] + carH / 2);
        glEnd();
    }

    // COIN
    glColor4f(1, 1, 1, 1);
    glBindTexture(GL_TEXTURE_2D, g_coinTex);
    glBegin(GL_QUADS);
    glTexCoord2f(0, 0); glVertex2f(coinX, coinY + carW / 5);
    glTexCoord2f(1, 0); glVertex2f(coinX + carW / 5, coinY);
    glTexCoord2f(1, 1); glVertex2f(coinX, coinY - carW / 5);
    glTexCoord2f(0, 1); glVertex2f(coinX - carW / 5, coinY);
    glEnd();

    // EXPLOSION
    glColor4f(1, 1, 1, 1);
    glBindTexture(GL_TEXTURE_2D, g_explosionTex);
    glBegin(GL_QUADS);
    glTexCoord2f(0, 0); glVertex2f(explosionX, explosionY + carW);
    glTexCoord2f(1, 0); glVertex2f(explosionX + carW, explosionY);
    glTexCoord2f(1, 1); glVertex2f(explosionX, explosionY - carW);
    glTexCoord2f(0, 1); glVertex2f(explosionX - carW, explosionY);
    glEnd();

    char scoreStr[64];
    sprintf_s(scoreStr, "Coins: %d", score);
    RenderText(-0.95f, 0.9f, scoreStr);

    sprintf_s(scoreStr, "Kills: %d", kills);
    RenderText(-0.95f, 0.8f, scoreStr);

    sprintf_s(scoreStr, "Lives: %d", lives);
    RenderText(-0.95f, 0.6f, scoreStr);

    char timeStr[64];
    sprintf_s(timeStr, "Time: %.2f", gameTime);

    // Рисуем в правом верхнем углу
    RenderText(0.55f, 0.9f, timeStr);

    SwapBuffers(g_hDC);
}


extern "C" __declspec(dllexport)
void ResizeGL(int width, int height)
{
    if (height <= 0) height = 1;
    glViewport(0, 0, width, height);
}

extern "C" __declspec(dllexport)
void CleanupOpenGL()
{
    wglMakeCurrent(nullptr, nullptr);

    if (g_hRC)
    {
        wglDeleteContext(g_hRC);
        g_hRC = nullptr;
    }

    if (g_hDC && g_hWnd)
    {
        ReleaseDC(g_hWnd, g_hDC);
        g_hDC = nullptr;
    }
    if (g_fontBase != 0)
    {
        glDeleteLists(g_fontBase, 96);
        g_fontBase = 0;
    }
}
