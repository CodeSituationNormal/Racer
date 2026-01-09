using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.IO;

namespace RacerWF
{
    public partial class Form1 : Form
    {
        const int EnemyTypeCount = 5;

        [DllImport("RacerRender.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool InitOpenGL(IntPtr hwnd, int w, int h);
        [DllImport("RacerRender.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool InitFont();
        [DllImport("RacerRender.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool InitTextures();
        [DllImport("RacerRender.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void RenderFrame(
            float playerX, float offset,
            float[] enemiesX, float[] enemiesY,
            int[] enemiesType, int enemyCount,
            float coinX, float coinY,
            int score, int kills,
            float explosionX, float explosionY,
            float gameTime, int lives);

        [DllImport("RacerRender.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void ResizeGL(int w, int h);
        [DllImport("RacerRender.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void CleanupOpenGL();

        OpenGLHost glHost;
        System.Windows.Forms.Timer timer;
        Random rnd = new Random();

        // Player car parameters
        float playerX = 0;
        float playerY = -0.75f;
        float playerVX = 0;
        float playerMass = 800f;

        float carSpeed = 0;
        float acceleration = 0.0001f;
        const float MaxSpeed = 0.022f;

        // Road offset
        float roadOffset = 0;

        // Enemies' cars parameters
        float[] enemiesX = new float[EnemyTypeCount];
        float[] enemiesY = new float[EnemyTypeCount] { 5f, 5f, 5f, 5f, 5f};
        float[] enemiesVX = new float[EnemyTypeCount];
        float[] enemiesSpeed = new float[EnemyTypeCount];
        float[] enemiesMass = new float[EnemyTypeCount];
        bool[] enemiesAlive = new bool[EnemyTypeCount];
        int[] enemiesType = new int[EnemyTypeCount];
        bool[] enemyColliding = new bool[EnemyTypeCount];

        // Rewards
        int[] enemyReward = new int[EnemyTypeCount] { 1, 2, 2, 2, 3 };

        // Coins parameters
        float coinX, coinY = 1.5f;
        bool coinAlive;
        
        // Explosion parameters
        float explosionX, explosionY = 1.5f;
        bool explosionAlive;

        // Scores
        int score = 0;
        int kills = 0;
        int doomCounter = 0;

        int recordCoins = 0;
        int recordKills = 0;
        float recordTime = 0f;

        string recordsFilePath => Path.Combine(Application.StartupPath, "records.txt");

        bool left, right, up, down;

        float gameTime = 0f;

        Panel menuPanel;
        Panel pausePanel;
        Panel resultsPanel;
        Button btnStart;
        Button btnExit;
        Panel recordsPanel;
        Label lblResultText;
        Label lblRecordInfo;

        public Form1()
        {
            InitializeComponent();

            glHost = new OpenGLHost { Dock = DockStyle.Fill };
            Controls.Add(glHost);

            CreateMenu();
            CreatePauseMenu();
            CreateResultsPanel();
            CreateRecordsPanel();
            LoadRecords();

            timer = new System.Windows.Forms.Timer { Interval = 16 };
            timer.Tick += GameLoop;

            KeyPreview = true;

            KeyDown += (s, e) =>
            {
                if (menuPanel.Visible)
                {
                    if (e.KeyCode == Keys.Escape)
                        Close();
                    return;
                }

                if (pausePanel.Visible)
                {
                    if (e.KeyCode == Keys.Escape)
                        ResumeGame();
                    return;
                }

                if (e.KeyCode == Keys.Escape)
                {
                    PauseGame();
                    return;
                }

                SetKey(e.KeyCode, true);
            };
            KeyUp += (_, e) => SetKey(e.KeyCode, false);
            glHost.Resize += (_, __) => ResizeGL(glHost.Width, glHost.Height);
            FormClosing += (_, __) => CleanupOpenGL();
        }

        void LoadRecords()
        {
            if (!File.Exists(recordsFilePath))
            {
                SaveRecords();
                return;
            }

            try
            {
                var lines = File.ReadAllLines(recordsFilePath);
                if (lines.Length >= 3)
                {
                    recordCoins = int.TryParse(lines[0], out int c) ? c : 0;
                    recordKills = int.TryParse(lines[1], out int k) ? k : 0;
                    recordTime = float.TryParse(lines[2], out float t) ? t : 0f;
                }
            }
            catch
            {
                recordCoins = recordKills = 0;
                recordTime = 0f;
                SaveRecords(); 
            }
        }

        void SaveRecords()
        {
            try
            {
                File.WriteAllLines(recordsFilePath, new string[]
                {
            recordCoins.ToString(),
            recordKills.ToString(),
            recordTime.ToString("F2")
                });
            }
            catch
            {}
        }

        void CreateResultsPanel()
        {
            resultsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                Visible = false
            };

            lblResultText = new Label
            {
                Font = new Font("Arial", 16),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.TopCenter,
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 120
            };

            Button btnRestart = new Button
            {
                Text = "RESTART",
                ForeColor = Color.White,
                Font = new Font("Arial", 14),
                Size = new Size(200, 40)
            };
            btnRestart.Click += (s, e) =>
            {
                resultsPanel.Visible = false;
                RestartGame();
            };

            Button btnToMainMenu = new Button
            {
                Text = "MAIN MENU",
                ForeColor = Color.White,
                Font = new Font("Arial", 14),
                Size = new Size(200, 40)
            };
            btnToMainMenu.Click += (s, e) => 
            {
                resultsPanel.Visible = false;
                GoToMainMenu();
            };

            Button btnExit = new Button
            {
                Text = "EXIT",
                Font = new Font("Arial", 14),
                ForeColor = Color.White,
                Size = new Size(200, 40)
            };
            btnExit.Click += (s, e) => Close();

            resultsPanel.Resize += (s, e) =>
            {
                int totalHeight = lblResultText.Height + btnRestart.Height + btnToMainMenu.Height + btnExit.Height + 60;
                int startY = (resultsPanel.ClientSize.Height - totalHeight) / 2;

                lblResultText.Location = new Point(0, startY);
                btnRestart.Location = new Point((resultsPanel.ClientSize.Width - btnRestart.Width) / 2, startY + lblResultText.Height + 10);
                btnToMainMenu.Location = new Point((resultsPanel.ClientSize.Width - btnToMainMenu.Width) / 2, btnRestart.Bottom + 10);
                btnExit.Location = new Point((resultsPanel.ClientSize.Width - btnExit.Width) / 2, btnToMainMenu.Bottom + 10);
            };

            resultsPanel.Controls.Add(lblResultText);
            resultsPanel.Controls.Add(btnRestart);
            resultsPanel.Controls.Add(btnToMainMenu);
            resultsPanel.Controls.Add(btnExit);

            Controls.Add(resultsPanel);
            resultsPanel.BringToFront();
        }

        void CreateMenu()
        {
            menuPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                Visible = true
            };

            Label lblTitle = new Label
            {
                Text = "RACER",
                Font = new Font("Arial", 32, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 100
            };

            btnStart = new Button
            {
                Text = "START",
                Font = new Font("Arial", 16),
                ForeColor = Color.White,
                Size = new Size(200, 50)
            };
            btnStart.Click += StartGame;

            Button btnRecords = new Button
            {
                Text = "RECORD TABLES",
                Font = new Font("Arial", 16),
                ForeColor = Color.Yellow,
                Size = new Size(200, 50)
            };
            btnRecords.Click += (s, e) =>
            {
                lblRecordInfo.Text = $"RECORDS\n\n" +
                                     $"Max Coins: {recordCoins}\n" +
                                     $"Max Kills: {recordKills}\n" +
                                     $"Max Time:  {recordTime:F2} s";

                menuPanel.Visible = false;
                recordsPanel.Visible = true;
                recordsPanel.BringToFront();
            };

            btnExit = new Button
            {
                Text = "EXIT",
                Font = new Font("Arial", 16),
                ForeColor = Color.Red,
                Size = new Size(200, 50)
            };
            btnExit.Click += (s, e) => Close();

            menuPanel.Resize += (s, e) =>
            {
                int totalHeight = btnStart.Height + btnRecords.Height + btnExit.Height + 30;
                int startY = (menuPanel.ClientSize.Height - totalHeight) / 2;

                btnStart.Location = new Point((menuPanel.ClientSize.Width - btnStart.Width) / 2, startY);
                btnRecords.Location = new Point((menuPanel.ClientSize.Width - btnRecords.Width) / 2, startY + btnStart.Height + 10);
                btnExit.Location = new Point((menuPanel.ClientSize.Width - btnExit.Width) / 2, startY + btnStart.Height + btnRecords.Height + 20);
            };

            menuPanel.Controls.Add(lblTitle);
            menuPanel.Controls.Add(btnExit);
            menuPanel.Controls.Add(btnStart);
            menuPanel.Controls.Add(btnRecords);
            Controls.Add(menuPanel); 
            menuPanel.BringToFront();
        }

        void StartGame(object sender, EventArgs e)
        {
            menuPanel.Visible = false;

            if (!InitOpenGL(glHost.Handle, glHost.Width, glHost.Height) ||
                !InitFont() || !InitTextures())
            {
                MessageBox.Show("OpenGL init failed!");
                Close();
                return;
            }

            ResetGame();

            timer.Start();
        }


        void ResetGame()
        {
            playerX = 0;
            carSpeed = 0;
            roadOffset = 0;
            score = 0;
            kills = 0;
            doomCounter = 0;
            gameTime = 0f;

            for (int i = 0; i < EnemyTypeCount; i++)
            {
                enemiesAlive[i] = false;
                enemyColliding[i] = false;
            }

            coinAlive = false;
            explosionAlive = false;

            left = right = up = down = false;
        }
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (!InitOpenGL(glHost.Handle, glHost.Width, glHost.Height) ||
                !InitFont() || !InitTextures())
            {
                Close();
                return;
            }
        }

        void CreateRecordsPanel()
        {
            recordsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                Visible = false
            };

            Label lblTitle = new Label
            {
                Text = "RECORDS",
                Font = new Font("Arial", 28, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.TopCenter,
                Height = 60
            };
            lblTitle.Dock = DockStyle.Top;

            lblRecordInfo = new Label
            {
                Font = new Font("Arial", 16),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.TopCenter,
                AutoSize = false,
                Padding = new Padding(20)
            };
            lblRecordInfo.Dock = DockStyle.Fill;

            Button btnToMainMenu = new Button
            {
                Text = "MAIN MENU",
                ForeColor = Color.White,
                Font = new Font("Arial", 14),
                Size = new Size(200, 40),
                Dock = DockStyle.Bottom
            };
            btnToMainMenu.Click += (s, e) =>
            {
                recordsPanel.Visible = false;
                GoToMainMenu();
            };

            recordsPanel.Resize += (s, e) =>
            {
                btnToMainMenu.Location = new Point(
                    (recordsPanel.ClientSize.Width - btnToMainMenu.Width) / 2,
                    recordsPanel.ClientSize.Height - btnToMainMenu.Height - 30
                );
            };

            recordsPanel.Controls.Add(lblTitle);
            recordsPanel.Controls.Add(lblRecordInfo);
            recordsPanel.Controls.Add(btnToMainMenu);

            Controls.Add(recordsPanel);
            recordsPanel.BringToFront();
        }

        void CreatePauseMenu()
        {
            pausePanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(128, 0, 0, 0), 
                Visible = false 
            };

            Label lblPause = new Label
            {
                Text = "PAUSE",
                Font = new Font("Arial", 28, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.TopCenter,
                Dock = DockStyle.Top,
                Height = 80
            };

            Button btnResume = new Button
            {
                Text = "RESUME",
                Font = new Font("Arial", 14),
                Size = new Size(200, 40)
            };
            btnResume.Click += (s, e) => ResumeGame();

            Button btnRestart = new Button
            {
                Text = "RESTART",
                Font = new Font("Arial", 14),
                Size = new Size(200, 40)
            };
            btnRestart.Click += (s, e) => RestartGame();

            Button btnToMainMenu = new Button
            {
                Text = "MAIN MENU",
                Font = new Font("Arial", 14),
                Size = new Size(200, 40)
            };
            btnToMainMenu.Click += (s, e) => GoToMainMenu();

            pausePanel.Resize += (s, e) =>
            {
                int totalHeight = btnResume.Height + btnRestart.Height + btnToMainMenu.Height + 40; 
                int startY = (pausePanel.ClientSize.Height - totalHeight) / 2;

                btnResume.Location = new Point((pausePanel.ClientSize.Width - btnResume.Width) / 2, startY);
                btnRestart.Location = new Point((pausePanel.ClientSize.Width - btnRestart.Width) / 2, startY + btnResume.Height + 10);
                btnToMainMenu.Location = new Point((pausePanel.ClientSize.Width - btnToMainMenu.Width) / 2, startY + btnResume.Height + btnRestart.Height + 20);
            };

            pausePanel.Controls.Add(lblPause);
            pausePanel.Controls.Add(btnResume);
            pausePanel.Controls.Add(btnRestart);
            pausePanel.Controls.Add(btnToMainMenu);

            Controls.Add(pausePanel);
            pausePanel.BringToFront();
        }

        void PauseGame()
        {
            if (timer.Enabled)
            {
                timer.Stop();
                pausePanel.Visible = true;
                pausePanel.BringToFront();
            }
        }

        void ResumeGame()
        {
            pausePanel.Visible = false;
            timer.Start();
        }

        void RestartGame()
        {
            pausePanel.Visible = false;
            ResetGame(); 
            timer.Start();
        }

        void GoToMainMenu()
        {
            pausePanel.Visible = false;
            menuPanel.Visible = true;
            menuPanel.BringToFront();
            timer.Stop();
            ResetGame();
        }

        void GameLoop(object sender, EventArgs e)
        {
            gameTime += 0.016f;
            if (up) carSpeed = Math.Min(MaxSpeed, carSpeed + acceleration);
            if (down) carSpeed = Math.Max(0, carSpeed - acceleration * 10);

            playerVX = 0;
            if (left) playerVX -= carSpeed;
            if (right) playerVX += carSpeed;

            playerX += playerVX;

            float minX = -0.65f + 0.16f;
            float maxX = 0.65f - 0.16f;
            playerX = Math.Clamp(playerX, minX, maxX);

            roadOffset += carSpeed;

            TrySpawnEnemies(minX, maxX);
            UpdateEnemies(minX, maxX);

            UpdateCoin();
            UpdateExplosion();

            RenderFrame(
                playerX, roadOffset,
                enemiesX, enemiesY,
                enemiesType, CountAliveEnemies(),
                coinX, coinY,
                score, kills,
                explosionX, explosionY,
                gameTime, 5 - doomCounter);
        }

        void TrySpawnEnemies(float minX, float maxX)
        {
            for (int i = 0; i < EnemyTypeCount; i++)
            {
                if (enemiesAlive[i]) continue;

                float typeProbability = 0.02f - i * 0.01f;
                if (rnd.NextSingle() < typeProbability) continue;

                enemiesAlive[i] = true;
                enemiesX[i] = minX + rnd.NextSingle() * (maxX - minX);

                bool spawnTop = rnd.Next(0, 2) == 0;
                if (spawnTop)
                {
                    enemiesY[i] = 1.5f + rnd.NextSingle() * (3.0f - 1.5f);
                }
                else
                {
                    enemiesY[i] = -3.0f + rnd.NextSingle() * (-1.5f + 3.0f); 
                }

                enemiesType[i] = i;

                switch (i)
                {
                    case 0: enemiesMass[i] = 1000f; break;
                    case 1: enemiesMass[i] = 1500f; break;
                    case 2: enemiesMass[i] = 2000f; break;
                    case 3: enemiesMass[i] = 2500f; break;
                    case 4: enemiesMass[i] = 3000f; break;
                }

                float minSpeed = 0.002f + i * 0.001f;
                float maxSpeed = 0.004f + i * 0.002f;
                float speed = minSpeed + rnd.NextSingle() * (maxSpeed - minSpeed) * (2000 / enemiesMass[i]);
                enemiesSpeed[i] = speed;

                enemiesVX[i] = 0;
            }
        }


        void UpdateEnemies(float minX, float maxX)
        {
            const float halfW = 0.08f;
            const float halfH = 0.10f;
            float e = 0.1f;

            for (int i = 0; i < EnemyTypeCount; i++)
            {
                if (!enemiesAlive[i]) continue;

                enemiesY[i] += enemiesSpeed[i];
                enemiesY[i] -= carSpeed;
                enemiesX[i] += enemiesVX[i];
                enemiesVX[i] *= 0.95f;

                bool collisionX = Math.Abs(playerX - enemiesX[i]) < halfW * 2;
                bool collisionY = Math.Abs(playerY - enemiesY[i]) < halfH * 2;

                // Collision physics
                if (collisionX && collisionY)
                {
                    float dx = enemiesX[i] - playerX;
                    float dy = enemiesY[i] - playerY;
                    float overlapX = (halfW * 2) - Math.Abs(dx);
                    float overlapY = (halfH * 2) - Math.Abs(dy);
                    bool sideHit = overlapX < overlapY;
                    bool frontalHit = !sideHit;
                    float signX = Math.Sign(dx);
                    float totalMass = playerMass + enemiesMass[i];

                    if (sideHit)
                    {
                        playerX -= overlapX * (enemiesMass[i] / totalMass) * signX;
                        enemiesX[i] += overlapX * (playerMass / totalMass) * signX;
                        float v1 = playerVX;
                        float v2 = enemiesVX[i];
                        playerVX =
                            (playerMass * v1 + enemiesMass[i] * v2 +
                             enemiesMass[i] * e * (v2 - v1)) / totalMass;
                        enemiesVX[i] =
                            (playerMass * v1 + enemiesMass[i] * v2 +
                             playerMass * e * (v1 - v2)) / totalMass;
                    }

                    if (frontalHit)
                    {
                        if (!enemyColliding[i])
                        {
                            float v1 = carSpeed;
                            float v2 = enemiesSpeed[i];
                            carSpeed =
                                (playerMass * v1 + enemiesMass[i] * v2 +
                                 enemiesMass[i] * e * (v2 - v1)) / totalMass;
                            enemiesSpeed[i] =
                                (playerMass * v1 + enemiesMass[i] * v2 +
                                 playerMass * e * (v1 - v2)) / totalMass;
                            doomCounter++;
                            enemyColliding[i] = true;

                        }
                        if (doomCounter >= 5)
                        {
                            timer.Stop();
                            if (score > recordCoins) recordCoins = score;
                            if (kills > recordKills) recordKills = kills;
                            if (gameTime > recordTime) recordTime = gameTime;

                            SaveRecords();
                            string resultText = $"SCORES:\n" +
                                                $"{score} coins & {kills} kills\n" +
                                                $"in {gameTime:F2} s";

                            lblResultText.Text = resultText;
                            resultsPanel.Visible = true;
                            resultsPanel.BringToFront();
                            return;
                        }
                    }
                }
                else
                {
                    enemyColliding[i] = false;
                }

                if (enemiesY[i] < -3.0f || enemiesY[i] > 5.0f)
                {
                    enemiesAlive[i] = false; 
                }
                else if (enemiesX[i] < minX || enemiesX[i] > maxX)
                {
                    explosionX = enemiesX[i];
                    explosionY = enemiesY[i];
                    explosionAlive = true;
                    score += enemyReward[i];
                    kills++;
                    enemiesAlive[i] = false;
                }
                for (int j = 0; j < EnemyTypeCount; j++)
                {
                    bool collisionEX = Math.Abs(enemiesX[j] - enemiesX[i]) < halfW * 2;
                    bool collisionEY = Math.Abs(enemiesY[j] - enemiesY[i]) < halfH * 2;

                    if (collisionEX && collisionEY)
                    {
                        float dx = enemiesX[i] - enemiesX[j];
                        float dy = enemiesY[i] - enemiesY[j];
                        float overlapX = (halfW * 2) - Math.Abs(dx);
                        float overlapY = (halfH * 2) - Math.Abs(dy);
                        bool sideHit = overlapX < overlapY;
                        bool frontalHit = !sideHit;
                        float signX = Math.Sign(dx);
                        float totalMass = enemiesMass[j] + enemiesMass[i];

                        if (sideHit)
                        {
                            enemiesX[j] -= overlapX * (enemiesMass[i] / totalMass) * signX;
                            enemiesX[i] += overlapX * (enemiesMass[j] / totalMass) * signX;
                            float v1 = enemiesVX[j];
                            float v2 = enemiesVX[i];
                            enemiesVX[j] =
                                (enemiesMass[j] * v1 + enemiesMass[i] * v2 +
                                    enemiesMass[i] * e * (v2 - v1)) / totalMass;
                            enemiesVX[i] =
                                (enemiesMass[j] * v1 + enemiesMass[i] * v2 +
                                    enemiesMass[j] * e * (v1 - v2)) / totalMass;
                        }

                        if (frontalHit)
                        {
                            float v1 = enemiesSpeed[j];
                            float v2 = enemiesSpeed[i];
                            enemiesSpeed[j] =
                                (enemiesMass[j] * v1 + enemiesMass[i] * v2 +
                                    enemiesMass[i] * e * (v2 - v1)) / totalMass;
                            enemiesSpeed[i] =
                                (enemiesMass[j] * v1 + enemiesMass[i] * v2 +
                                    enemiesMass[j] * e * (v1 - v2)) / totalMass;
                        }
                    }
                }
            }
        }

        void UpdateCoin()
        {
            if (!coinAlive)
            {
                coinAlive = true;
                coinX = rnd.NextSingle() - 0.5f;
                coinY = 1.5f;
            }

            coinY -= carSpeed;

            if (Math.Abs(playerX - coinX) < 0.08f &&
                Math.Abs(playerY - coinY) < 0.1f)
            {
                score++;
                coinAlive = false;
            }

            if (coinY < -1.2f) coinAlive = false;
        }

        void UpdateExplosion()
        {
            if (!explosionAlive) return;
            explosionY -= carSpeed;
            if (explosionY < -1.2f) explosionAlive = false;
        }

        int CountAliveEnemies()
        {
            int c = 0;
            for (int i = 0; i < EnemyTypeCount; i++)
                if (enemiesAlive[i]) c++;
            return c;
        }

        void SetKey(Keys k, bool v)
        {
            if (k == Keys.Left || k == Keys.A) left = v;
            if (k == Keys.Right || k == Keys.D) right = v;
            if (k == Keys.Up || k == Keys.W) up = v;
            if (k == Keys.Down || k == Keys.S) down = v;
        }
    }
}
