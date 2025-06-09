using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.IO;
using Newtonsoft.Json;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using System.Reflection.Emit;
using System.Media;
using System.Windows.Media.Media3D;
using System.Reflection;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

namespace WpfGame
{
    public partial class MainWindow : Window
    {
        private SoundManager soundManager;
        private BackgroundManager backgroundManager;
        private MediaPlayer menuMusicPlayer = new MediaPlayer();
        public Player player;
        private List<Entity> entities = new List<Entity>();
        private List<Platform> platforms = new List<Platform>();
        private List<ParallaxLayer> parallaxLayers = new List<ParallaxLayer>();
        private GameTimer gameTimer;
        private Level currentLevel;
        private List<Item> items = new List<Item>();
        public List<Enemy> enemies = new List<Enemy>();

        // настройки камеры
        private double CameraZoom = 2.0; // Увеличение в 2 раза
        private ScaleTransform cameraScaleTransform;
        // Камера
        private TranslateTransform cameraTransform;
        private const double CameraFollowSpeed = 0.1;
        private Rect levelBounds = new Rect(0, 0, 4000, 800); // Границы уровня

        private bool isLevelUpOpen;
        private int pendingSkillPoints;

        private LevelExit levelExit;
        private int currentLevelNumber = 1;
        private bool isTransitioning;
        private bool GameStarted = false;

        public MainWindow()
        {
            InitializeComponent();
            InitializeMenu();
            //Loaded += MainWindow_Loaded;
        }

        private void InitializeMenu()
        {
            // Показываем меню и скрываем игровые элементы
            MainMenu.Visibility = Visibility.Visible;
            GameCanvas.Visibility = Visibility.Collapsed;
            UiCanvas.Visibility = Visibility.Collapsed;
            BlackOverlay.Visibility = Visibility.Collapsed;
            DeathOverlay.Visibility = Visibility.Collapsed;
            DeathText.Visibility = Visibility.Collapsed;

            // Загрузка и воспроизведение музыки меню
            string menuMusicPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "menu.mp3"
            );
            if (File.Exists(menuMusicPath))
            {
                menuMusicPlayer.Open(new Uri(menuMusicPath));
                menuMusicPlayer.MediaEnded += (s, e) =>
                {
                    menuMusicPlayer.Position = TimeSpan.Zero;
                    menuMusicPlayer.Play();
                };
                menuMusicPlayer.Play();
            }
        }

        private void StartNewGame_Click(object sender, RoutedEventArgs e)
        {
            // остановка фоновой
            menuMusicPlayer.Stop();
            // Скрываем меню
            MainMenu.Visibility = Visibility.Collapsed;
            GameStarted = true;

            // Инициализируем игру
            try
            {
                BlackOverlay.Visibility = Visibility.Visible;
                DeathOverlay.Visibility = Visibility.Visible;
                DeathText.Visibility = Visibility.Visible;
                WatermarkText.Visibility = Visibility.Collapsed;

                soundManager = new SoundManager();
                soundManager.PlayBackground();
                InitializeCamera();
                InitializeGame();
                InitializePlayer();
                LoadLevel(currentLevelNumber);
                LoadGameAssets();

                // Запускаем анимацию и игровой цикл
                Dispatcher.BeginInvoke(
                    new Action(() =>
                {
                    StartFadeInAnimation();
                    StartGameLoop();
                    GameCanvas.Visibility = Visibility.Visible;
                    UiCanvas.Visibility = Visibility.Visible;
                }), DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации: {ex.Message}");
                Close();
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void WatermarkText_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/VAUsIGT",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии ссылки: {ex.Message}");
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                BlackOverlay.Visibility = Visibility.Visible;

                soundManager = new SoundManager();
                soundManager.PlayBackground();
                InitializeCamera();
                InitializeGame();
                InitializePlayer(); 
                // Загрузка первого уровня перед загрузкой ресурсов
                LoadLevel(currentLevelNumber);
                LoadGameAssets();
                // Запускаем анимацию после полной загрузки
                Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                    StartFadeInAnimation();
                    StartGameLoop();
                    }),
                    DispatcherPriority.Loaded
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing game: {ex.Message}");
                Close();
            }
        }

        private void LoadLevel(int levelNumber)
        {
            string levelPath = System.IO.Path.Combine(
    AppDomain.CurrentDomain.BaseDirectory,
    "Levels",
    $"level{levelNumber}.json"
);

            if (!File.Exists(levelPath))
            {
                throw new FileNotFoundException($"Level file not found: {levelPath}");
            }

            string levelJson = File.ReadAllText(levelPath);
            currentLevel = JsonConvert.DeserializeObject<Level>(levelJson) ?? CreateDefaultLevel();
        }

        private void InitializeCamera()
        {
            cameraTransform = new TranslateTransform();
            cameraScaleTransform = new ScaleTransform(CameraZoom, CameraZoom);

            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(cameraScaleTransform); // Сначала масштаб
            transformGroup.Children.Add(cameraTransform);       // Затем смещение

            GameCanvas.RenderTransform = transformGroup;
        }

        private void LoadGameAssets()
        {
            // Обновляем размер затемнения
            BlackOverlay.Width = ActualWidth;
            BlackOverlay.Height = ActualHeight;

            // Загрузка фона
            string bgConfigPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Assets\\Backgrounds",
                currentLevel.BackgroundConfig
            );

            backgroundManager.LoadBackground(bgConfigPath);

            player.Position = new Point(640, 360 + 550);
            player.UpdatePosition();
            player.ResetState();

            // Создание платформ
            if (currentLevel.Platforms == null)
            {
                currentLevel.Platforms = new List<PlatformData>();
            }

            // Создание платформ
            foreach (var platformData in currentLevel.Platforms)
            {
                var platform = new Platform(
                    new Rect(platformData.X, platformData.Y, platformData.Width, platformData.Height),
                    platformData.Type
                );
                platforms.Add(platform);
                GameCanvas.Children.Add(platform.HitboxVisual); // Добавляем хитбокс
            }
            // Загрузка Entity
            if (currentLevel.Entities == null)
            {
                currentLevel.Entities = new List<EntityData>();
            }

            foreach (var entityData in currentLevel.Entities)
            {
                string texturePath = System.IO.Path.Combine(
    AppDomain.CurrentDomain.BaseDirectory,
    "Assets",
    "Entities",
    entityData.Texture
);
                var entity = new Entity(
                    new Rect(entityData.X, entityData.Y, entityData.Width, entityData.Height),
                    texturePath,
                    entityData.IsDestructible,
                    entityData.IsCollidable,
                    entityData.ZIndex,
                    entityData.DestructionSound,
                    entityData.Drops
                );
                entities.Add(entity);
                GameCanvas.Children.Add(entity.Image);
                GameCanvas.Children.Add(entity.HitboxVisual); // Добавляем хитбокс
            }

            // Загрузка шаблонов врагов
            string templatesPath = System.IO.Path.Combine(
    AppDomain.CurrentDomain.BaseDirectory,
    "Data",
    "enemy_templates.json"
);
            if (!File.Exists(templatesPath))
            {
                throw new FileNotFoundException($"Enemy templates file not found: {templatesPath}");
            }

            string templatesJson = File.ReadAllText(templatesPath);
            var enemyTemplates = JsonConvert.DeserializeObject<Dictionary<string, EnemyData>>(templatesJson);

            // Создание врагов
            foreach (var enemyInLevel in currentLevel.Enemies)
            {
                if (!enemyTemplates.TryGetValue(enemyInLevel.Type, out var template))
                {
                    MessageBox.Show($"Enemy template {enemyInLevel.Type} not found!");
                    continue;
                }

                // Создаем анимации из шаблона
                var animationse = new Dictionary<string, Animation>();
                foreach (var anim in template.Animations)
                {
                    var animation = CreateEnemyAnimation(anim.Value);
                    animationse.Add(anim.Key.ToLower(), animation);
                }

                // Создаем врага с параметрами из шаблона
                var enemy = new Enemy(
                    new Point(enemyInLevel.X, enemyInLevel.Y),
                    animationse,
                    "idle",
                    template.SpriteWidth,
                    template.SpriteHeight,
                    template.HitboxWidth,
                    template.HitboxHeight,
                    template.Speed,
                    template.Damage,
                    template.Health,
                    template.AttackRange,
                    template.DetectionRange,
                    template.AttackCooldown,
                    player
                );

                enemies.Add(enemy);
                enemy.AddToCanvas(GameCanvas);
            }
            // Загрузка перехода между уровнями
            var exitEntity = currentLevel.Entities.FirstOrDefault(e => e.IsLevelExit);
            if (exitEntity != null)
            {
                levelExit = new LevelExit(new Rect(exitEntity.X, exitEntity.Y, exitEntity.Width, exitEntity.Height));
                GameCanvas.Children.Add(levelExit.Visual);
            }
        }

        private void InitializePlayer()
        {
            // Загрузка спрайт-листов
            var idleSprite = LoadSprite("player_idle.png");
            var runSprite = LoadSprite("player_run.png");
            var jumpSprite = LoadSprite("player_jump.png");
            var attackSprite = LoadSprite("player_attack.png");
            var deadSprite = LoadSprite("player_dead.png");

            // Инициализация анимаций
            var animations = new Dictionary<string, Animation>
            {
                {"idle", new Animation(idleSprite, 64, 80, 4, 0.1)},
                {"run", new Animation(runSprite, 80, 80, 8, 0.08)},
                {"jump", new Animation(jumpSprite, 80, 80, 8, 1.15)},
                {"attack", new Animation(attackSprite, 96, 80, 8, 0.0625)},
                {"dead", new Animation(deadSprite, 80, 50, 8, 0.1)}
            };

            player = new Player(new Point(640, 360 + 550), animations, soundManager, items, GameCanvas); // Смещение на 500px вниз (изначально высоко спавн)
        }

        private void StartFadeOut()
        {
            if (isTransitioning) return;
            isTransitioning = true;

            var fadeAnimation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = TimeSpan.FromSeconds(1),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };

            LoadNextLevel();
            BlackOverlay.BeginAnimation(OpacityProperty, fadeAnimation);
        }

        private async void LoadNextLevel()
        {
            // запуск затемнения
            StartFadeOutAnimation();
            // ожидание завершения
            await Task.Delay(1500);
            // сохраняем прогресс
            var savedPlayer = new
            {
                Level = player.Level,
                Experience = player.Experience,
                Health = player.Health,
                BaseMaxHealth = player.BaseMaxHealth,
                BaseMaxStamina = player.BaseMaxStamina,
                BaseDamage = player.BaseDamage,
                BaseSpeed = player.BaseSpeed,
                AvailableSkillPoints = player.AvailableSkillPoints,
                MainItem = player.MainItem,
                SecondaryItem = player.SecondaryItem
            };
            // очистка уровня
            ClearCurrentLevel();

            // загрузка уровня
            currentLevelNumber++;
            LoadLevel(currentLevelNumber);
            LoadGameAssets();
            // восстановление прогресса
            player.Level = savedPlayer.Level;
            player.Experience = savedPlayer.Experience;
            player.Health = savedPlayer.Health;
            player.BaseMaxHealth = savedPlayer.BaseMaxHealth;
            player.BaseMaxStamina = savedPlayer.BaseMaxStamina;
            player.BaseDamage = savedPlayer.BaseDamage;
            player.BaseSpeed = savedPlayer.BaseSpeed;
            player.AvailableSkillPoints = savedPlayer.AvailableSkillPoints;
            player.MainItem = savedPlayer.MainItem;
            player.SecondaryItem = savedPlayer.SecondaryItem;
            // перезагрузка ресурсов
            //LoadGameAssets();                                //////////////////////////////////////// вернуть мб

            // запуск анимации появления
            StartFadeInAnimation();
            isTransitioning = false;
        }

        private void StartFadeInAnimation()
        {
            BlackOverlay.Visibility = Visibility.Visible;
            var fadeAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromSeconds(5),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            fadeAnimation.Completed += (s, e) =>
            {
                BlackOverlay.Visibility = Visibility.Collapsed;
            };

            BlackOverlay.BeginAnimation(OpacityProperty, fadeAnimation);
        }
        private void StartFadeOutAnimation()
        {
            BlackOverlay.Visibility = Visibility.Visible;
            var fadeAnimation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = TimeSpan.FromSeconds(2),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            fadeAnimation.Completed += (s, e) =>
            {
                BlackOverlay.Visibility = Visibility.Visible;
            };

            BlackOverlay.BeginAnimation(OpacityProperty, fadeAnimation);
        }

        private void ClearCurrentLevel()
        {
            // Удаление врагов
            foreach (var enemy in enemies.ToArray())
            {
                enemy.RemoveFromCanvas(GameCanvas);
                enemies.Remove(enemy);
            }
            enemies.Clear();

            // Удаление объектов
            foreach (var entity in entities)
            {
                GameCanvas.Children.Remove(entity.Image);
                GameCanvas.Children.Remove(entity.HitboxVisual);
            }
            entities.Clear();

            // Удаление платформ
            foreach (var platform in platforms)
            {
                GameCanvas.Children.Remove(platform.HitboxVisual);
            }
            platforms.Clear();

            // Очистка предметов
            foreach (var item in items)
            {
                GameCanvas.Children.Remove(item.Image);
                GameCanvas.Children.Remove(item.HitboxVisual);
            }
            items.Clear();

            if (levelExit != null)
            {
                GameCanvas.Children.Remove(levelExit.Visual);
                levelExit = null;
            }
        }

        private BitmapImage LoadEnemySprite(string filename)
        {
            string fullPath = filename;
            return new BitmapImage(new Uri(fullPath));
        }

        private Animation CreateEnemyAnimation(EnemyAnimationData animData)
        {
            var sprite = LoadEnemySprite(System.IO.Path.Combine(
    AppDomain.CurrentDomain.BaseDirectory,
    "Assets",
    animData.Texture
));
            return new Animation(
                sprite,
                animData.FrameWidth,
                animData.FrameHeight,
                animData.FramesCount,
                animData.FrameDuration
            )
            {
                IsLooping = animData.IsLooping
            };
        }

        private Level CreateDefaultLevel()
        {
            return new Level
            {
                Platforms = new List<PlatformData>
            {
                new PlatformData { X = 0, Y = 1040, Width = 4000, Height = 120, Type = "ground" }
            }
            };
        }

        private BitmapImage LoadSprite(string filename)
        {
            string fullPath = System.IO.Path.Combine(
    AppDomain.CurrentDomain.BaseDirectory,
    "Assets",
    "Player",
    filename
);
            return new BitmapImage(new Uri(fullPath));
        }

        private void InitializeGame()
        {
            backgroundManager = new BackgroundManager(GameCanvas);

            gameTimer = new GameTimer();
            gameTimer.Update += GameUpdate;
            gameTimer.Render += GameRender;
        }

        private void StartGameLoop() => gameTimer.Start();

        private void GameUpdate(double deltaTime)
        {
            if (isTransitioning) return;
            if (player.IsDead)
            {
                if (player.deathAnimationStarted)
                {
                    player.Update(deltaTime, platforms, entities);
                    GameRender();
                    ShowDeathEffects();
                }
                return;
            }
            player.Update(deltaTime, platforms, entities);
            soundManager.UpdateAttack(player.Attack);
            UpdateCamera();
            UpdateParallax();
            CheckInput();

            // Удаляем уничтоженные объекты
            for (int i = entities.Count - 1; i >= 0; i--)
            {
                if (entities[i].IsDestroyed)
                {
                    GameCanvas.Children.Remove(entities[i].Image);
                    GameCanvas.Children.Remove(entities[i].HitboxVisual);
                    entities.RemoveAt(i);
                }
            }
            // Обновляем предметы
            foreach (var item in items.ToArray())
            {
                item.Update(platforms);

                // Удаляем упавшие за пределы уровня
                if (item.Position.Y > levelBounds.Bottom + 1000)
                {
                    items.Remove(item);
                    GameCanvas.Children.Remove(item.Image);
                    GameCanvas.Children.Remove(item.HitboxVisual);
                }
                // Если подобран
                if (item.IsCollected)
                {
                    items.Remove(item);
                    GameCanvas.Children.Remove(item.Image);
                    GameCanvas.Children.Remove(item.HitboxVisual);
                }
            }
            foreach (var enemy in enemies)
            {
                enemy.Update(deltaTime, platforms, entities);
            }

            // Проверка перехода на новый уровень
            if (levelExit != null && player.GetPlayerRect().IntersectsWith(levelExit.Bounds))
            {
                bool allEnemiesDead = enemies.All(e => e.IsDead);

                if (allEnemiesDead)
                {
                    StartFadeOut();
                }
                else
                {
                    // Показать сообщение "Убить всех врагов!"
                }
            }
        }

        private void ShowDeathEffects()
        {
            // Анимация затемнения экрана
            var overlayAnim = new DoubleAnimation(0.7, TimeSpan.FromSeconds(1.5));
            DeathOverlay.BeginAnimation(OpacityProperty, overlayAnim);

            // Анимация текста (только появление)
            var textAnim = new DoubleAnimation(1, TimeSpan.FromSeconds(1.5));
            DeathText.BeginAnimation(OpacityProperty, textAnim);
        }

        private void ShowLevelUpPanel()
        {
            if (player.IsDead) return;

            isLevelUpOpen = true;
            LevelUpPanel.Visibility = Visibility.Visible;
            pendingSkillPoints = player.AvailableSkillPoints;

            // Обновляем значения
            HealthStat.Text = player.BaseMaxHealth.ToString();
            StaminaStat.Text = player.BaseMaxStamina.ToString("0");
            DamageStat.Text = player.BaseDamage.ToString();
            SpeedStat.Text = player.BaseSpeed.ToString("0.0");

            // Сбрасываем бонусы
            HealthBonus.Text = "";
            StaminaBonus.Text = "";
            DamageBonus.Text = "";
            SpeedBonus.Text = "";

            UpdateSkillPointsDisplay();
            UpdatePlusButtons();
        }
        private void UpdateSkillPointsDisplay()
        {
            SkillPointsText.Text = $"Очков: {pendingSkillPoints}";
            ApplyButton.IsEnabled = pendingSkillPoints < player.AvailableSkillPoints;
        }

        private void UpdatePlusButtons()
        {
            bool hasPoints = pendingSkillPoints > 0;
            HealthPlus.IsEnabled = hasPoints;
            StaminaPlus.IsEnabled = hasPoints;
            DamagePlus.IsEnabled = hasPoints;
            SpeedPlus.IsEnabled = hasPoints;
        }
        private void StatPlus_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            string stat = (string)button.Tag;

            player.AddTempBonus(stat);
            pendingSkillPoints--;

            // Обновляем отображение бонусов
            switch (stat)
            {
                case "Health":
                    HealthBonus.Text = $"(+{player.tempHealthBonus})";
                    HealthStat.Text = (player.BaseMaxHealth + player.tempHealthBonus).ToString();
                    break;
                case "Stamina":
                    StaminaBonus.Text = $"(+{player.tempStaminaBonus})";
                    StaminaStat.Text = (player.BaseMaxStamina + player.tempStaminaBonus).ToString("0");
                    break;
                case "Damage":
                    DamageBonus.Text = $"(+{player.tempDamageBonus})";
                    DamageStat.Text = (player.BaseDamage + player.tempDamageBonus).ToString();
                    break;
                case "Speed":
                    SpeedBonus.Text = $"(+{player.tempSpeedBonus:0.0})";
                    SpeedStat.Text = (player.BaseSpeed + player.tempSpeedBonus).ToString("0.0");
                    break;
            }

            UpdateSkillPointsDisplay();
            UpdatePlusButtons();
        }

        private void ApplyLevelUp_Click(object sender, RoutedEventArgs e)
        {
            player.AvailableSkillPoints -= (player.AvailableSkillPoints - pendingSkillPoints);
            player.ApplyLevelUp();
            //CloseLevelUpPanel();
        }

        private void CloseLevelUp_Click(object sender, RoutedEventArgs e)
        {
            CloseLevelUpPanel();
        }

        private void CloseLevelUpPanel()
        {
            isLevelUpOpen = false;
            LevelUpPanel.Visibility = Visibility.Collapsed;
            player.tempHealthBonus = 0;
            player.tempStaminaBonus = 0;
            player.tempDamageBonus = 0;
            player.tempSpeedBonus = 0;
        }

        private void UpdateCamera()
        {
            // Расчет целевой позиции камеры
            double targetX = -player.Position.X * CameraZoom + 1000 + ActualWidth / 2;
            double targetY = -player.Position.Y * CameraZoom + ActualHeight / 2;

            // Ограничение границ уровня
            double minX = -levelBounds.Width * CameraZoom + ActualWidth;
            double maxX = 0;
            targetX = Clamp(targetX, minX, maxX);

            double minY = -levelBounds.Height * CameraZoom + ActualHeight;
            double maxY = 0;
            targetY = Clamp(targetY, minY, maxY);

            // Плавное движение камеры
            cameraTransform.X += (targetX - cameraTransform.X) * CameraFollowSpeed;
            cameraTransform.Y += (targetY - cameraTransform.Y) * CameraFollowSpeed;

            // Обновляем позицию затемнения
            Canvas.SetLeft(BlackOverlay, -cameraTransform.X / CameraZoom);
            Canvas.SetTop(BlackOverlay, -cameraTransform.Y / CameraZoom);
        }

        private double Clamp(double value, double min, double max)
        {
            return value < min ? min : (value > max ? max : value);
        }

        private void GameRender()
        {
            var translateTransform = (TranslateTransform)Player.RenderTransform;

            // Размеры спрайта из настроек персонажа (работает как сдвиг в сторону)
            double spriteWidth = player.Size.Width + 90; // 128 из кода
            double spriteHeight = player.Size.Height - 40; // 160

            // Центр персонажа в мировых координатах
            double centerX = player.Position.X - spriteWidth / 2;
            double centerY = player.Position.Y - spriteHeight / 2;

            // отражение уже учтено в GetCurrentFrame()
            translateTransform.X = centerX;
            translateTransform.Y = centerY;

            Player.Source = player.CurrentAnimation.GetCurrentFrame(!player.FacingRight);

            // Обновление здоровья
            double healthPercent = player.Health / (double)player.BaseMaxHealth;
            double maxMainWidth = 298 - 2; // 296px
            double currentMainWidth = maxMainWidth * healthPercent;

            var geometry = new PathGeometry();
            var figure = new PathFigure { StartPoint = new Point(2, 2) };

            figure.Segments.Add(new LineSegment(new Point(2 + currentMainWidth, 2), true));
            figure.Segments.Add(new LineSegment(new Point(
                2 + currentMainWidth + 20 * (currentMainWidth / maxMainWidth),
                12), true));
            figure.Segments.Add(new LineSegment(new Point(
                2 + currentMainWidth + 20 * (currentMainWidth / maxMainWidth),
                18), true));
            figure.Segments.Add(new LineSegment(new Point(2 + currentMainWidth, 28), true));
            figure.Segments.Add(new LineSegment(new Point(2, 28), true));
            figure.IsClosed = true;
            geometry.Figures.Add(figure);
            HealthBarInner.Data = geometry;
            // Обновление стамины
            double staminaPercent = player.Stamina / player.BaseMaxStamina;
            double maxStaminaWidth = 198 - 2; // 196px
            double currentStaminaWidth = maxStaminaWidth * staminaPercent;

            var staminaGeometry = new PathGeometry();
            var staminaFigure = new PathFigure { StartPoint = new Point(2, 2) };

            staminaFigure.Segments.Add(new LineSegment(new Point(2 + currentStaminaWidth, 2), true));
            staminaFigure.Segments.Add(new LineSegment(new Point(
                2 + currentStaminaWidth + 10 * (currentStaminaWidth / maxStaminaWidth),
                7), true));
            staminaFigure.Segments.Add(new LineSegment(new Point(
                2 + currentStaminaWidth + 10 * (currentStaminaWidth / maxStaminaWidth),
                12), true));
            staminaFigure.Segments.Add(new LineSegment(new Point(2 + currentStaminaWidth, 17), true));
            staminaFigure.Segments.Add(new LineSegment(new Point(2, 17), true));
            staminaFigure.IsClosed = true;

            staminaGeometry.Figures.Add(staminaFigure);
            StaminaBarInner.Data = staminaGeometry;

            UpdateInventoryUI();
            UpdateLevelUI();
        }

        private void UpdateLevelUI()
        {
            // Текст уровня
            LevelText.Text = player.Level.ToString();

            // Рассчитываем угол для шкалы опыта
            double expPercent = (double)player.Experience / player.RequiredExperience;
            double angle = 360 * expPercent;

            // Создаем геометрию для заполнения
            var geometry = new PathGeometry();
            var figure = new PathFigure
            {
                StartPoint = new Point(40, 5), // Верхняя точка круга
                IsClosed = false
            };

            // Добавляем дугу
            figure.Segments.Add(new ArcSegment(
                point: CalculatePoint(angle),
                size: new Size(35, 35),
                rotationAngle: 0,
                isLargeArc: angle > 180,
                sweepDirection: SweepDirection.Clockwise,
                isStroked: true));

            geometry.Figures.Add(figure);
            ExperienceBar.Data = geometry;
        }

        private Point CalculatePoint(double angle)
        {
            double radians = (angle - 90) * Math.PI / 180; // Смещаем начало на 12 часов
            double x = 40 + 35 * Math.Cos(radians);
            double y = 40 + 35 * Math.Sin(radians);
            return new Point(x, y);
        }

        private void UpdateInventoryUI()
        {
            // Основной слот
            InventoryMainImage.Source = player.MainItem?.Texture;
            InventoryMainImage.Visibility = player.MainItem != null
                ? Visibility.Visible
                : Visibility.Collapsed;

            // Дополнительный слот
            InventorySecondaryImage.Source = player.SecondaryItem?.Texture;
            InventorySecondaryImage.Visibility = player.SecondaryItem != null
                ? Visibility.Visible
                : Visibility.Collapsed;

            // Анимация при использовании
            if (player.MainItem == null)
            {
                InventoryMainSlot.BorderBrush = Brushes.Gray;
            }
            else
            {
                InventoryMainSlot.BorderBrush = Brushes.White;
            }
        }

        private void UpdateParallax()
        {
            backgroundManager.Update(cameraTransform.X);
            // Обновляем позиции Entity
            foreach (var entity in entities)
            {
                entity.UpdatePosition(cameraTransform.X);
            }
            // Обновляем хитбоксы платформ
            foreach (var platform in platforms)
            {
                platform.UpdatePosition(cameraTransform.X);
            }
            foreach (var item in items)
            {
                // Временная подсветка предметов
                item.HitboxVisual.Stroke = Brushes.Lime;
                item.HitboxVisual.StrokeThickness = 3;
                // Отладочный вывод позиций
                Console.WriteLine($"Item position: {item.Position.X}, {item.Position.Y}");
                Console.WriteLine($"Canvas coords: {Canvas.GetLeft(item.HitboxVisual)}, {item.Position.X + cameraTransform.X}, {item.Position.Y + cameraTransform.Y}");

                item.UpdatePosition(cameraTransform.X, cameraTransform.Y);

                Canvas.SetLeft(item.HitboxVisual, item.Position.X);
                Canvas.SetTop(item.HitboxVisual, item.Position.Y);
            }
        }

        private void CheckInput()
        {
            if (player.IsDead) return;
            player.MoveLeft = Keyboard.IsKeyDown(Key.A);
            player.MoveRight = Keyboard.IsKeyDown(Key.D);
            player.Jump = Keyboard.IsKeyDown(Key.Space);
            player.Attack = player.isAttacking;
            if (Keyboard.IsKeyDown(Key.Q))
            {
                player.UseMainItem();
            }

            if (Keyboard.IsKeyDown(Key.Tab))
            {
                if (player.TrySwapItems())
                {
                    soundManager.PlaySwap();
                }
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Close();
            if (!GameStarted) { return; }
            if (e.Key == Key.E && !isLevelUpOpen)
            {
                ShowLevelUpPanel();
            }
            if (e.Key == Key.H) // H - показать/скрыть хитбоксы
            {
                foreach (var enemy in enemies)
                {
                    enemy.HitboxVisual.Visibility =
                        enemy.HitboxVisual.Visibility == Visibility.Collapsed
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                }
                foreach (var entiti in entities)
                {
                    entiti.HitboxVisual.Visibility =
                        entiti.HitboxVisual.Visibility == Visibility.Collapsed
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                }
                foreach (var item in items)
                {
                    item.HitboxVisual.Visibility =
                        item.HitboxVisual.Visibility == Visibility.Collapsed
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                }
                foreach (var platform in platforms)
                {
                    platform.HitboxVisual.Visibility =
                        platform.HitboxVisual.Visibility == Visibility.Collapsed
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                }
            }

            //масштабирование камеры
            if (e.Key == Key.Add)
                CameraZoom *= 1.1;
            if (e.Key == Key.Subtract)
                CameraZoom *= 0.9;

            cameraScaleTransform.ScaleX = CameraZoom;
            cameraScaleTransform.ScaleY = CameraZoom;
        }
        // обработчик клика на уровень
        private void LevelText_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!isLevelUpOpen)
            {
                ShowLevelUpPanel();
            }
        }

        private void Window_KeyUp(object sender, KeyEventArgs e) { }
    }

    public class EnemyAnimationData
    {
        public string Texture { get; set; }
        public int FrameWidth { get; set; } = 112;
        public int FrameHeight { get; set; } = 80;
        public int FramesCount { get; set; } = 1;
        public double FrameDuration { get; set; } = 0.1;
        public bool IsLooping { get; set; } = true;
    }

    public class BackgroundManager
    {
        private readonly Canvas canvas;
        private readonly List<BackgroundLayer> layers = new List<BackgroundLayer>();

        public BackgroundManager(Canvas gameCanvas)
        {
            canvas = gameCanvas;
        }

        public void LoadBackground(string configPath)
        {
            ClearExistingLayers();

            if (!File.Exists(configPath))
                throw new FileNotFoundException($"Background config not found: {configPath}");

            var configJson = File.ReadAllText(configPath);
            var config = JsonConvert.DeserializeObject<BackgroundConfig>(configJson);

            foreach (var layerConfig in config.Layers.OrderBy(l => l.ZIndex))
            {
                AddLayer(layerConfig);
            }
        }

        private void AddLayer(LayerConfig config)
        {
            string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets\\Backgrounds", config.Texture);
            var layer = new BackgroundLayer(fullPath, config.Speed, config.TileCount);
            layers.Add(layer);

            foreach (var img in layer.Tiles)
            {
                Canvas.SetZIndex(img, config.ZIndex);
                canvas.Children.Insert(0, img);
            }
        }

        private void ClearExistingLayers()
        {
            foreach (var layer in layers)
            {
                foreach (var tile in layer.Tiles)
                {
                    canvas.Children.Remove(tile);
                }
            }
            layers.Clear();
        }

        public void Update(double playerPositionX)
        {
            foreach (var layer in layers)
            {
                layer.Update(playerPositionX);
            }
        }
    }
    public class BackgroundConfig
    {
        public List<LayerConfig> Layers { get; set; } = new List<LayerConfig>();
    }

    public class LayerConfig
    {
        public string Texture { get; set; }
        public double Speed { get; set; }
        public int TileCount { get; set; } = 6;
        public int ZIndex { get; set; } = 0;
    }

    public class BackgroundLayer
    {
        private readonly List<Image> tiles = new List<Image>();
        private readonly double speedFactor;
        private readonly double tileWidth;

        public List<Image> Tiles => tiles;

        public BackgroundLayer(string imagePath, double speed, int tileCount)
        {
            speedFactor = speed;
            var bitmap = new BitmapImage(new Uri(imagePath, UriKind.RelativeOrAbsolute));
            tileWidth = bitmap.PixelWidth + 340;

            for (int i = 0; i < tileCount; i++)
            {
                var img = new Image
                {
                    Source = bitmap,
                    Stretch = Stretch.Uniform,
                    Width = tileWidth,
                    RenderTransform = new TranslateTransform()
                };
                tiles.Add(img);
            }
        }

        public void Update(double cameraX)
        {
            double offset = cameraX * speedFactor;

            foreach (var tile in tiles)
            {
                var transform = (TranslateTransform)tile.RenderTransform;
                transform.X = offset;
                offset += tileWidth;

                // Бесконечный повтор
                if (transform.X + tileWidth < 0)
                    transform.X += tileWidth * tiles.Count;
            }
        }
    }

    public class Level
    {
        public string BackgroundConfig { get; set; } = "default_bg.json";
        public List<PlatformData> Platforms { get; set; } = new List<PlatformData>();
        public List<EntityData> Entities { get; set; } = new List<EntityData>();
        public List<EnemyInLevel> Enemies { get; set; } = new List<EnemyInLevel>();
    }

    public class PlatformData
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string Type { get; set; }
    }

    public class Player
    {
        // базовые характеристики
        [JsonProperty]
        public int Level { get; set; } = 1;
        [JsonProperty]
        public double Experience { get; set; }
        [JsonProperty]
        public int BaseMaxHealth { get; set; } = 100;
        [JsonProperty]
        public double BaseMaxStamina { get; set; } = 100;
        [JsonProperty]
        public int BaseDamage { get; set; } = 10;
        [JsonProperty]
        public double BaseSpeed { get; set; } = 8;
        // значения для прокачки
        public int tempHealthBonus;
        public int tempStaminaBonus;
        public int tempDamageBonus;
        public double tempSpeedBonus;

        public int Health { get; set; }
        public double Stamina { get; set; }
        public int Damage => BaseDamage + tempDamageBonus;
        public double MoveSpeed => BaseSpeed + tempSpeedBonus;

        private SoundManager soundManager;
        private bool wasJumping;
        public Point Position { get; set; }
        public Size Size { get; } = new Size(30, 115); // Соответствует реальному размеру спрайта персонажа 128 160 70
        public bool IsDead { get; private set; }
        private bool deathAnimationPlayed;
        public bool deathAnimationStarted;
        public double MaxStamina { get; } = 100;
        private const double StaminaCost = 30; // Расход за атаку
        private const double StaminaRegen = 2; // Восстановление в секунду
        public bool staminaBlocked = false;
        public Item MainItem { get;set; }
        public Item SecondaryItem { get;set; }
        private double _speedMultiplier = 1.0;
        private DateTime _speedBoostEndTime;
        private DateTime _lastSwapTime = DateTime.MinValue;
        private readonly TimeSpan _swapCooldown = TimeSpan.FromSeconds(0.5);
        private Vector _velocity;
        public Vector Velocity
        {
            get => _velocity;
            set => _velocity = value;
        }
        public bool IsGrounded { get; set; }
        public bool FacingRight { get; set; } = true;
        public Animation CurrentAnimation { get; private set; }

        public bool MoveLeft { get; set; }
        public bool MoveRight { get; set; }
        public bool Jump { get; set; }
        public bool Attack { get; set; }

        public bool isAttacking = false;
        private double attackTimer = 0.0;
        private const double attackDuration = 0.5; // Длительность атаки в секундах
        private bool attackStarted = false;

        private DateTime lastAttackTime;
        private const double AttackCooldown = attackDuration;

        private readonly Dictionary<string, Animation> animations;
        private readonly List<Item> worldItems;
        private readonly Canvas gameCanvas;
        private const double JumpForce = -10;
        private const double Gravity = 0.8;
        public int RequiredExperience => 25 * (int)Math.Pow(2, Level); //50 100 200 400 
        public int AvailableSkillPoints { get; set; }

        public Player(Point startPosition, Dictionary<string, Animation> animations, SoundManager soundManager, List<Item> items, Canvas canvas)
        {
            Position = startPosition;
            this.animations = animations;
            CurrentAnimation = animations["idle"];
            this.soundManager = soundManager;
            this.worldItems = items;
            this.gameCanvas = canvas;

            // Инициализация здоровья и стамины
            Health = BaseMaxHealth;
            Stamina = BaseMaxStamina;
        }

        public void UpdatePosition()
        {
            // Сбрасываем временные модификаторы
            _speedMultiplier = 1.0;
            _speedBoostEndTime = DateTime.MinValue;

            // Сбрасываем анимации
            CurrentAnimation = animations["idle"];
            CurrentAnimation.Reset();
        }
        public void ResetState()
        {
            // Сброс временных состояний
            Health = BaseMaxHealth;
            Stamina = BaseMaxStamina;
            _velocity = new Vector(0, 0);
            IsDead = false;
            deathAnimationStarted = false;
            CurrentAnimation = animations["idle"];
            CurrentAnimation.Reset();
        }

        private void CheckDeath()
        {
            if (Health <= 0 && !IsDead)
            {
                IsDead = true;
                deathAnimationStarted = true;
                CurrentAnimation = animations["dead"];
                CurrentAnimation.Reset();
                Velocity = new Vector(0, JumpForce / 2); // Небольшой подброс
                soundManager.PlayDeath();
            }
        }

        public void Update(double deltaTime, List<Platform> platforms, List<Entity> entities)
        {
            if (IsDead)
            {
                ApplyPhysics(deltaTime);
                CheckCollisions(platforms, entities);

                CurrentAnimation.Update(deltaTime);
                // Проверяем завершение анимации
                if (deathAnimationStarted && CurrentAnimation.CurrentFrame >= CurrentAnimation.FramesCount - 1)
                {
                    deathAnimationStarted = false;
                }
                return;
            }

            // Обновление таймера атаки
            if (isAttacking)
            {
                attackTimer -= deltaTime;
                if (attackTimer <= 0)
                {
                    isAttacking = false;
                }
            }

            // Обработка начала атаки
            if (attackStarted)
            {
                HandleAttack(entities);
                attackStarted = false;
            }

            CheckDeath();
            UpdateStamina(deltaTime);
            HandleInput();
            ApplyPhysics(deltaTime);
            CheckCollisions(platforms, entities);
            CheckItemCollisions(worldItems);
            UpdateAnimationState();
            CurrentAnimation.Update(deltaTime);

            if (Attack && (DateTime.Now - lastAttackTime).TotalSeconds > AttackCooldown)
            {
                HandleAttack(entities);
                lastAttackTime = DateTime.Now;
            }
            if (DateTime.Now > _speedBoostEndTime)
            {
                _speedMultiplier = 1.0;
            }
        }

        private void UpdateStamina(double deltaTime)
        {
            if (Attack && !staminaBlocked)
            {
                Stamina = Math.Max(0, Stamina - StaminaCost * deltaTime);
                if (Stamina <= 0) staminaBlocked = true;
            }
            else
            {
                Stamina = Math.Min(MaxStamina, Stamina + StaminaRegen * deltaTime);
                if (Stamina >= MaxStamina * 0.2) staminaBlocked = false;
            }
        }

        private void HandleAttack(List<Entity> entities)
        {
            if (staminaBlocked) return;//если устал
            Rect attackArea = GetAttackArea();

            // Атака по Entity
            foreach (var entity in entities.ToArray())
            {
                if (entity.IsDestroyed ||
                    !entity.IsDestructible ||
                    !entity.Bounds.IntersectsWith(attackArea)) continue;

                entity.Destroy(worldItems, gameCanvas);
                entities.Remove(entity);
            }
            // Атака по врагам
            foreach (var enemy in ((MainWindow)Application.Current.MainWindow).enemies.ToArray())
            {
                if (enemy.Bounds.IntersectsWith(attackArea) && enemy.Health > 0)
                {
                    enemy.TakeDamage(Damage);
                }
            }
        }

        private Rect GetAttackArea()
        {
            double attackRange = 50;
            double x = FacingRight ?
                Position.X + Size.Width / 2 :
                Position.X - Size.Width / 2 - attackRange;

            return new Rect(
                x,
                Position.Y - Size.Height / 2,
                attackRange,
                Size.Height
            );
        }

        private void HandleInput()
        {
            if (isAttacking)
            {
                // Блокируем управление во время атаки
                Velocity = new Vector(0, Velocity.Y);
                return;
            }

            var velocity = Velocity;
            velocity.X = 0;

            if (MoveLeft)
            {
                velocity.X = -MoveSpeed * _speedMultiplier;
                FacingRight = false;
            }
            if (MoveRight)
            {
                velocity.X = MoveSpeed * _speedMultiplier;
                FacingRight = true;
            }
            if (Jump && IsGrounded)
            {
                soundManager.PlayJump(true);
                velocity.Y = JumpForce;
                IsGrounded = false;
            }
            wasJumping = Jump;
            // Начало атаки
            if (Keyboard.IsKeyDown(Key.J) && !staminaBlocked && (DateTime.Now - lastAttackTime).TotalSeconds >= AttackCooldown)
            {
                isAttacking = true;
                attackTimer = attackDuration;
                lastAttackTime = DateTime.Now;
                attackStarted = true;
            }
            Velocity = velocity;
        }

        public void UseMainItem()
        {
            if (MainItem == null) return;

            // Определяем тип предмета по текстуре
            // зелье скорости
            if (MainItem.Texture.UriSource.ToString().Contains("speed_potion.png"))
            {
                _speedMultiplier = 1.5;
                _speedBoostEndTime = DateTime.Now.AddMinutes(1);
                soundManager.PlayPotionUse();
            }
            // зелье здоровья
            else if (MainItem.Texture.UriSource.ToString().Contains("health_potion.png"))
            {
                Health = Math.Min(BaseMaxHealth, Health + 70);
                soundManager.PlayPotionUse();
            }
            // зелье выносливости
            else if (MainItem.Texture.UriSource.ToString().Contains("stamina_potion.png"))
            {
                Stamina = Math.Min(BaseMaxStamina, Stamina + 70);
                soundManager.PlayPotionUse();
            }

            MainItem = null;
        }

        public bool TrySwapItems()
        {
            if ((DateTime.Now - _lastSwapTime) < _swapCooldown)
                return false;

            SwapItems();
            _lastSwapTime = DateTime.Now;
            return true;
        }
        public void SwapItems()
        {
            (MainItem, SecondaryItem) = (SecondaryItem, MainItem);
        }

        private void ApplyPhysics(double deltaTime)
        {
            var velocity = Velocity;
            velocity.Y += Gravity;
            Velocity = velocity;

            Position = new Point(
                Position.X + velocity.X,
                Position.Y + velocity.Y
            );
        }

        private void CheckCollisions(List<Platform> platforms, List<Entity> entities)
        {
            var playerRect = GetPlayerRect();

            CheckPlatformCollisions(platforms, playerRect);
            CheckEntityCollisions(entities, playerRect);
        }

        public Rect GetPlayerRect()
        {
            return new Rect(
                Position.X - Size.Width / 2,
                Position.Y - Size.Height / 2,
                Size.Width,
                Size.Height
            );
        }

        private void CheckPlatformCollisions(List<Platform> platforms, Rect playerRect)
        {
            IsGrounded = false;

            foreach (var platform in platforms)
            {
                var platformRect = platform.Bounds;
                if (playerRect.IntersectsWith(platformRect))
                {
                    HandleCollision(platformRect);
                }
            }
        }

        private void CheckEntityCollisions(List<Entity> entities, Rect playerRect)
        {
            foreach (var entity in entities)
            {
                // Проверяем только Entity с коллизиями
                if (!entity.IsCollidable) continue;

                var entityRect = entity.Bounds;
                if (playerRect.IntersectsWith(entity.Bounds))
                {
                    HandleCollision(entityRect);
                    if (entity.IsDestructible)
                    {
                        // Логика разрушения объекта
                    }
                }
            }
        }

        private void CheckItemCollisions(List<Item> items)
        {
            var playerRect = GetPlayerRect();
            foreach (var item in items)
            {
                if (item.IsDestroyed || item.IsCollected) continue;

                if (playerRect.IntersectsWith(item.Bounds))
                {
                    if (TryAddItem(item))
                    {
                        item.IsCollected = true;
                    }
                }
            }
        }

        public bool TryAddItem(Item item)
        {
            if (MainItem == null)
            {
                MainItem = item;
                return true;
            }
            else if (SecondaryItem == null)
            {
                SecondaryItem = item;
                return true;
            }
            return false;
        }

        private void HandleCollision(Rect obstacleRect)
        {
            var playerRect = GetPlayerRect();

            // Расчет пересечений по осям
            double overlapX = Math.Min(playerRect.Right, obstacleRect.Right) -
                             Math.Max(playerRect.Left, obstacleRect.Left);
            double overlapY = Math.Min(playerRect.Bottom, obstacleRect.Bottom) -
                             Math.Max(playerRect.Top, obstacleRect.Top);

            if (overlapX > 0 && overlapY > 0)
            {
                if (overlapX < overlapY) // Горизонтальная коллизия
                {
                    if (playerRect.Left < obstacleRect.Left)
                    {
                        Position = new Point(Position.X - overlapX, Position.Y);
                        Velocity = new Vector(0, Velocity.Y);
                    }
                    else
                    {
                        Position = new Point(Position.X + overlapX, Position.Y);
                        Velocity = new Vector(0, Velocity.Y);
                    }
                }
                else // Вертикальная коллизия
                {
                    if (playerRect.Top < obstacleRect.Top)
                    {
                        Position = new Point(Position.X, Position.Y - overlapY);
                        Velocity = new Vector(Velocity.X, 0);
                        IsGrounded = true;
                    }
                    else
                    {
                        Position = new Point(Position.X, Position.Y + overlapY);
                        Velocity = new Vector(Velocity.X, 0);
                    }
                }
            }
        }

        private void UpdateAnimationState()
        {

            if (IsDead)
            {
                CurrentAnimation = animations["dead"];
                return;
            }

            if (Attack)
            {
                CurrentAnimation = animations["attack"];
                return;
            }
            else if (!IsGrounded)
            {
                CurrentAnimation = animations["jump"];
            }
            else if (Velocity.X != 0)
            {
                CurrentAnimation = animations["run"];

            }
            else
            {
                CurrentAnimation = animations["idle"];
            }

            soundManager.UpdateRunning(IsGrounded && Velocity.X != 0 && !Attack);
            soundManager.UpdateAttack(Attack);
        }

        public void AddExperience(double amount)
        {
            if (IsDead) return;

            Experience += amount;

            while (Experience >= RequiredExperience)
            {
                Experience -= RequiredExperience;
                Level++;
                AvailableSkillPoints++;

                // добавить эффект нового уровня
            }
        }

        public void ApplyLevelUp()
        {
            BaseMaxHealth += tempHealthBonus;
            BaseMaxStamina += tempStaminaBonus;
            BaseDamage += tempDamageBonus;
            BaseSpeed += tempSpeedBonus;

            Health = BaseMaxHealth;
            Stamina = BaseMaxStamina;

            tempHealthBonus = 0;
            tempStaminaBonus = 0;
            tempDamageBonus = 0;
            tempSpeedBonus = 0;
        }
        public void AddTempBonus(string stat)
        {
            switch (stat)
            {
                case "Health":
                    tempHealthBonus += 20;
                    break;
                case "Stamina":
                    tempStaminaBonus += 20;
                    break;
                case "Damage":
                    tempDamageBonus += 5;
                    break;
                case "Speed":
                    tempSpeedBonus += 1;
                    break;
            }
        }
    }

    public class SoundManager
    {
        private readonly MediaPlayer backgroundPlayer = new MediaPlayer();
        private readonly SoundPlayer jumpSound;
        private readonly SoundPlayer runSound;
        private readonly SoundPlayer attackSound;
        private readonly SoundPlayer potionSound;
        private readonly SoundPlayer swapSound;
        private readonly SoundPlayer deathSound;

        private bool isRunning;
        private bool isAttacking;

        public SoundManager()
        {
            string selfpath = "Assets\\Player\\";
            // Загрузка звуков
            jumpSound = new SoundPlayer(selfpath + "jump.wav");
            runSound = new SoundPlayer(selfpath + "run.wav");
            attackSound = new SoundPlayer(selfpath + "attack.wav");
            deathSound = new SoundPlayer(selfpath + "dead.wav");

            potionSound = new SoundPlayer(selfpath + "potion_sound.wav");
            swapSound = new SoundPlayer(selfpath + "swap_sound.wav");

            // Настройка фоновой музыки
            string bgMusicPath = System.IO.Path.Combine(
    AppDomain.CurrentDomain.BaseDirectory,
    "Assets",
    "Backgrounds",
    "BG1",
    "background.mp3"
);
            backgroundPlayer.Open(new Uri(bgMusicPath, UriKind.Absolute));
            backgroundPlayer.MediaEnded += (s, e) => backgroundPlayer.Position = TimeSpan.Zero;
        }

        public void PlayPotionUse() => potionSound.Play();
        public void PlaySwap() => swapSound.Play();

        public void PlayBackground()
        {
            backgroundPlayer.Play();
        }

        public void PlayJump(bool isJumping)
        {
            if (isJumping)
            {
                jumpSound.Play();
            }
        }

        public void UpdateRunning(bool isRunningNow)
        {
            if (isRunningNow && !isRunning)
            {
                runSound.PlayLooping();
                isRunning = true;
            }
            else if (!isRunningNow && isRunning)
            {
                runSound.Stop();
                isRunning = false;
            }
        }

        public void UpdateAttack(bool isAttackingNow)
        {
            if (isAttackingNow && !isAttacking)
            {
                attackSound.PlayLooping();
                isAttacking = true;
            }
            else if (!isAttackingNow && isAttacking)
            {
                attackSound.Stop();
                isAttacking = false;
            }
        }

        public void PlayDeath()
        {
            backgroundPlayer.Stop();
            deathSound.Play();
        }
    }

    public class Animation
    {
        private readonly BitmapSource spriteSheet;
        private readonly int frameWidth;
        private readonly int frameHeight;
        private readonly int framesCount;
        private readonly double frameDuration;

        private double currentTime;
        private int currentFrame;

        public int CurrentFrame => currentFrame;
        public int FramesCount => framesCount;

        public bool IsLooping { get; set; } = true;
        public bool IsCompleted { get; private set; }

        public void Reset()
        {
            currentFrame = 0;
            currentTime = 0;
            IsCompleted = false;

        }

        public Animation(BitmapSource sprite, int frameWidth, int frameHeight,
                        int framesCount, double frameDuration)
        {
            spriteSheet = sprite;
            this.frameWidth = frameWidth;
            this.frameHeight = frameHeight;
            this.framesCount = framesCount;
            this.frameDuration = frameDuration;

            // проверка размеров
            //if (sprite.PixelWidth < frameWidth * framesCount)
            //{
            //    throw new ArgumentException("Sprite sheet width is too small for specified frames");
            //}
        }

        public ImageSource GetCurrentFrame(bool flipHorizontal)
        {
            try
            {
                var rect = new Int32Rect(
                    currentFrame * frameWidth,
                    0,
                    frameWidth,
                    frameHeight
                );

                var cropped = new CroppedBitmap(spriteSheet, rect);

                if (!flipHorizontal) return cropped;

                // Создаем flipped bitmap
                var flipped = new TransformedBitmap(
                    cropped,
                    new ScaleTransform(-1, 1, frameWidth / 2, frameHeight / 2)
                );

                return flipped;
            }
            catch
            {
                return new WriteableBitmap(1, 1, 96, 96, PixelFormats.Bgra32, null);
            }
        }

        private static WriteableBitmap CreateFlippedBitmap(BitmapSource source)
        {
            var flipped = new WriteableBitmap(source.PixelWidth, source.PixelHeight,
                96, 96, PixelFormats.Bgra32, null);

            flipped.Lock();
            try
            {
                int stride = source.PixelWidth * 4;
                byte[] pixels = new byte[stride * source.PixelHeight];
                source.CopyPixels(pixels, stride, 0);

                for (int y = 0; y < source.PixelHeight; y++)
                {
                    for (int x = 0; x < source.PixelWidth; x++)
                    {
                        int srcIndex = y * stride + x * 4;
                        int dstIndex = y * stride + (source.PixelWidth - 1 - x) * 4;

                        flipped.WritePixels(new Int32Rect(x, y, 1, 1),
                            pixels, srcIndex, dstIndex, 4);
                    }
                }
            }
            finally
            {
                flipped.Unlock();
            }

            return flipped;
        }

        public void Update(double deltaTime)
        {
            if (IsCompleted) return;

            currentTime += deltaTime;
            if (currentTime >= frameDuration)
            {
                currentFrame++;
                if (currentFrame >= framesCount)
                {
                    if (IsLooping)
                    {
                        currentFrame = 0;
                    }
                    else
                    {
                        currentFrame = framesCount - 1;
                        IsCompleted = true;
                    }
                }
                currentTime = 0;
            }
        }
    }

    public static class WriteableBitmapExtensions
    {
        public static void WritePixels(this WriteableBitmap bitmap, Int32Rect rect,
            byte[] buffer, int sourceIndex, int destinationIndex, int bytesPerPixel)
        {
            bitmap.WritePixels(rect, buffer, bitmap.BackBufferStride, 0);
        }
    }

    public class Platform
    {
        public Rect Bounds { get; }
        public string Type { get; }
        public Rectangle HitboxVisual { get; } // Хитбокс для отладки

        public Platform(Rect bounds, string type)
        {
            Bounds = bounds;
            Type = type;

            //Визуализация хитбокса
            HitboxVisual = new Rectangle
            {
                Width = bounds.Width,
                Height = bounds.Height,
                Stroke = Brushes.Blue,
                StrokeThickness = 2,
                Visibility = Visibility.Collapsed // Visible или Collapsed
            };
            Canvas.SetZIndex(HitboxVisual, 1000);
        }

        public void UpdatePosition(double cameraX)
        {
            Canvas.SetLeft(HitboxVisual, Bounds.X);
            Canvas.SetTop(HitboxVisual, Bounds.Y);
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class Item
    {
        [JsonProperty]
        public string TexturePath { get; private set; }
        public Point Position { get; set; }
        public Vector Velocity { get; set; } = new Vector(0, 0);
        public Image Image { get; }
        public Rectangle HitboxVisual { get; }
        public Rect Bounds => new Rect(Position.X, Position.Y, 30, 30);
        public bool IsDestroyed { get; private set; }
        public bool IsCollected { get; set; }
        public BitmapImage Texture { get; }

        private const double Gravity = 0.5;
        private const double GroundFriction = 0.8;

        public void UpdatePosition(double cameraX, double cameraY)
        {
            var transform = (TranslateTransform)Image.RenderTransform;
            transform.X = Position.X;
            transform.Y = Position.Y;

            Canvas.SetLeft(HitboxVisual, Position.X);
            Canvas.SetTop(HitboxVisual, Position.Y);
        }

        public Item(Point position, string texturePath)
        {
            TexturePath = texturePath;
            Position = position;
            Texture = new BitmapImage(new Uri(texturePath));
            // Загрузка текстуры с масштабированием до 40x40
            var bitmap = new BitmapImage(new Uri(texturePath));
            Image = new Image
            {
                Source = bitmap,
                Width = 30,
                Height = 30,
                RenderTransform = new TranslateTransform()
            };

            // Хитбокс для отладки
            HitboxVisual = new Rectangle
            {
                Width = 30,
                Height = 30,
                Stroke = Brushes.Green,
                StrokeThickness = 1,
                Visibility = Visibility.Collapsed
            };
            Canvas.SetZIndex(Image, 200);
            Canvas.SetZIndex(HitboxVisual, 1000);
        }

        public void Update(List<Platform> platforms)
        {
            if (IsDestroyed) return;

            // Применяем физику
            Velocity = new Vector(Velocity.X * GroundFriction, Velocity.Y + Gravity);
            Position = new Point(Position.X + Velocity.X, Position.Y + Velocity.Y);

            // Проверка коллизий с платформами
            var itemRect = Bounds;
            foreach (var platform in platforms)
            {
                if (itemRect.IntersectsWith(platform.Bounds))
                {
                    ResolveCollision(platform.Bounds);
                }
            }
        }

        private void ResolveCollision(Rect platformRect)
        {
            var itemRect = Bounds;
            double overlapX = Math.Min(itemRect.Right, platformRect.Right) - Math.Max(itemRect.Left, platformRect.Left);
            double overlapY = Math.Min(itemRect.Bottom, platformRect.Bottom) - Math.Max(itemRect.Top, platformRect.Top);

            if (overlapX > 0 && overlapY > 0)
            {
                if (overlapY < overlapX)
                {
                    // Вертикальная коллизия
                    if (itemRect.Top < platformRect.Top)
                    {
                        Position = new Point(Position.X, Position.Y - overlapY);
                        Velocity = new Vector(Velocity.X, 0);
                    }
                    else
                    {
                        Position = new Point(Position.X, Position.Y + overlapY);
                        Velocity = new Vector(Velocity.X, 0);
                    }
                }
            }
        }

        public void Destroy()
        {
            IsDestroyed = true;
            Image.Visibility = Visibility.Collapsed;
            HitboxVisual.Visibility = Visibility.Collapsed;
        }
    }

    public class DropData
    {
        public string Texture { get; set; }
        public int Count { get; set; }
        public double Chance { get; set; }
    }

    public class EntityData
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string Texture { get; set; }
        public bool IsDestructible { get; set; }
        public bool IsCollidable { get; set; } = true; // Значение по умолчанию
        public int ZIndex { get; set; } = 100; // Значение по умолчанию (как у игрока)
        public string DestructionSound { get; set; }
        public List<DropData> Drops { get; set; } = new List<DropData>();
        public bool IsLevelExit { get; set; }
    }

    public class Entity
    {
        public Rect Bounds { get; }
        public Image Image { get; }
        public bool IsDestructible { get; }
        public bool IsCollidable { get; }
        public Rectangle HitboxVisual { get; } // Хитбокс для отладки
        public int ZIndex { get; }
        public SoundPlayer DestructionSound { get; }
        public bool IsDestroyed { get; private set; }
        private List<DropData> drops;
        private static Random rnd = new Random();

        public Entity(Rect bounds, string texturePath, bool isDestructible, bool isCollidable, int zIndex, string destructionSound, List<DropData> drops)
        {
            ZIndex = zIndex;
            Bounds = bounds;
            IsDestructible = isDestructible;
            IsCollidable = isCollidable;
            this.drops = drops;

            Image = new Image
            {
                Source = new BitmapImage(new Uri(texturePath)),
                Width = bounds.Width,
                Height = bounds.Height,
                RenderTransform = new TranslateTransform()
            };
            // Визуализация хитбокса
            HitboxVisual = new Rectangle
            {
                Width = bounds.Width,
                Height = bounds.Height,
                Stroke = isCollidable ? Brushes.Red : Brushes.Yellow,
                StrokeThickness = 2,
                Visibility = Visibility.Collapsed // Для отключения: Visibility.Collapsed
            };
            Canvas.SetZIndex(HitboxVisual, zIndex);
            Canvas.SetZIndex(Image, zIndex);

            // Загрузка звука
            if (!string.IsNullOrEmpty(destructionSound))
            {
                string soundPath = System.IO.Path.Combine(
    AppDomain.CurrentDomain.BaseDirectory,
    "Assets",
    "Entities",
    "crate",
    destructionSound
);
                DestructionSound = new SoundPlayer(soundPath);
            }
        }

        public void Destroy(List<Item> items, Canvas canvas)
        {
            if (IsDestroyed) return;
            // Логирование разрушения
            Console.WriteLine($"Destroying entity at ({Bounds.X}, {Bounds.Y})");

            IsDestroyed = true;
            DestructionSound?.Play();

            // Помечаем для удаления
            Image.Visibility = Visibility.Collapsed;
            HitboxVisual.Visibility = Visibility.Collapsed;
            // Генерация дропа
            foreach (var drop in drops)
            {
                Console.WriteLine($"Processing drop: {drop.Texture} (count: {drop.Count}, chance: {drop.Chance})");
                for (int i = 0; i < drop.Count; i++)
                {
                    if (rnd.NextDouble() <= drop.Chance)
                    {
                        Console.WriteLine($"Spawning item: {drop.Texture}");
                        var itemPos = new Point(
                            Bounds.X + Bounds.Width / 2 - 20, // Центрируем предмет 40x40
                            Bounds.Y + Bounds.Height / 2 - 20
                        );

                        string texturePath = System.IO.Path.Combine(
    AppDomain.CurrentDomain.BaseDirectory,
    "Assets",
    "Items",
    drop.Texture
);
                        var item = new Item(itemPos, texturePath);

                        items.Add(item);
                        canvas.Children.Add(item.Image);
                        canvas.Children.Add(item.HitboxVisual);
                    }
                }
            }
        }

        public void UpdatePosition(double cameraX)
        {
            var transform = (TranslateTransform)Image.RenderTransform;
            transform.X = Bounds.X;
            transform.Y = Bounds.Y;

            Canvas.SetLeft(HitboxVisual, Bounds.X);
            Canvas.SetTop(HitboxVisual, Bounds.Y);
        }
    }

    public class ParallaxLayer
    {
        public BitmapImage Image { get; }
        public double SpeedFactor { get; }

        public ParallaxLayer(string imagePath, double speedFactor)
        {
            string fullPath = System.IO.Path.Combine(
    AppDomain.CurrentDomain.BaseDirectory,
    "Assets",
    "Backgrounds",
    "BG1",
    imagePath
);
            var uri = new Uri(fullPath, UriKind.Absolute);
            Image = new BitmapImage(uri);
            SpeedFactor = speedFactor;
        }
    }

    public class GameTimer
    {
        private readonly System.Windows.Threading.DispatcherTimer timer;
        public event Action<double> Update;
        public event Action Render;

        private DateTime lastUpdate;

        public GameTimer()
        {
            timer = new System.Windows.Threading.DispatcherTimer();
            timer.Tick += Timer_Tick;
            timer.Interval = TimeSpan.FromMilliseconds(16);
        }

        public void Start() => timer.Start();

        private void Timer_Tick(object sender, EventArgs e)
        {
            double deltaTime = (DateTime.Now - lastUpdate).TotalSeconds;
            lastUpdate = DateTime.Now;

            Update?.Invoke(deltaTime);
            Render?.Invoke();
        }
    }
    public class EnemyInLevel
    {
        public string Type { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }
    public class EnemyData
    {
        public double SpriteWidth { get; set; }
        public double SpriteHeight { get; set; }
        public double HitboxWidth { get; set; }
        public double HitboxHeight { get; set; }
        public double Speed { get; set; }
        public double Damage { get; set; }
        public double Health { get; set; }
        public double AttackRange { get; set; }
        public double DetectionRange { get; set; }
        public double AttackCooldown { get; set; }
        public Dictionary<string, EnemyAnimationData> Animations { get; set; }
    }
    public class Enemy
    {
        public enum EnemyState
        {
            Idle, Chasing, Attacking,
            Dead
        }
        public Rectangle HitboxVisual { get; } //хитбокс отладки
        public EnemyState CurrentState { get; private set; }
        public double Speed { get; }
        public double Damage { get; }
        public double Health { get; set; }
        public bool IsDead => Health <= 0;
        public bool IsDeath { get; private set; }
        public double AttackRange { get; }
        public double DetectionRange { get; }
        public double AttackCooldown { get; }
        private DateTime lastAttackTime = DateTime.MinValue;
        private DateTime attackStartTime;
        private bool isAttackInProgress;
        private const double AttackWindupTime = 0.5; // Задержка перед уроном в секундах
        private Player targetPlayer;
        public Point Position { get; set; }
        public Size SpriteSize { get; }
        public Size HitboxSize { get; }
        public Size Size { get; } = new Size(224, 160);
        public Animation CurrentAnimation { get; private set; }
        public bool FacingRight { get; set; }
        public Rect Bounds => new Rect(
        Position.X - HitboxSize.Width / 2,
        Position.Y - HitboxSize.Height / 2,
        HitboxSize.Width,
        HitboxSize.Height
        );

        private readonly Dictionary<string, Animation> animations;
        private readonly Image image;
        private const double Gravity = 0.8;
        private const double MoveSpeed = 3;
        public Vector Velocity { get; set; }
        public bool IsGrounded { get; private set; }
        private static MediaPlayer _hitSoundPlayer;


        public Enemy(Point position, Dictionary<string, Animation> animations, string defaultAnimation,
               double spriteWidth, double spriteHeight, double hitboxWidth, double hitboxHeight,
               double speed, double damage, double health, double attackRange, double detectionRange,
               double attackCooldown, Player player)
        {
            SpriteSize = new Size(spriteWidth, spriteHeight);
            HitboxSize = new Size(hitboxWidth, hitboxHeight);
            Position = new Point(
            position.X + hitboxWidth / 2,
            position.Y + hitboxHeight / 2
        );
            //Size = new Size(224, 160); // Размеры скелета
            this.animations = animations;
            CurrentAnimation = animations[defaultAnimation];

            image = new Image
            {
                Width = Size.Width,
                Height = Size.Height,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new TransformGroup()
                {
                    Children = new TransformCollection()
                {
                    new TranslateTransform()
                }
                }
            };

            Speed = speed;
            Damage = damage;
            Health = health;
            AttackRange = attackRange;
            DetectionRange = detectionRange;
            AttackCooldown = attackCooldown;
            targetPlayer = player;

            // хитбок отладки
            HitboxVisual = new Rectangle
            {
                Width = hitboxWidth,
                Height = hitboxHeight,
                Stroke = Brushes.Purple,
                StrokeThickness = 2,
                Fill = Brushes.Transparent,
                Visibility = Visibility.Collapsed // Для отладки
            };

            Canvas.SetZIndex(HitboxVisual, 999); // Выше других объектов
            Canvas.SetZIndex(image, 100);
        }

        public void Update(double deltaTime, List<Platform> platforms, List<Entity> entities)
        {
            if (IsDead)
            {
                if (!CurrentAnimation.IsCompleted)
                {
                    CurrentAnimation.Update(deltaTime);
                    image.Source = CurrentAnimation.GetCurrentFrame(FacingRight);
                }
                return;
            }
            // Логика AI
            UpdateAI(deltaTime);

            // Обновление анимации
            CurrentAnimation.Update(deltaTime);
            image.Source = CurrentAnimation.GetCurrentFrame(FacingRight);

            // Обновление позиции изображения
            var transform = (TransformGroup)image.RenderTransform;
            var translate = (TranslateTransform)transform.Children[0];
            translate.X = Position.X - SpriteSize.Width / 2;
            translate.Y = Position.Y - SpriteSize.Height / 2;

            // Обновляем позицию хитбокса
            Canvas.SetLeft(HitboxVisual, Position.X - HitboxSize.Width / 2);
            Canvas.SetTop(HitboxVisual, Position.Y - HitboxSize.Height / 2);

            // Физика
            ApplyPhysics(deltaTime);
            CheckCollisions(platforms, entities);
        }

        private void UpdateAI(double deltaTime)
        {
            if (IsDead || targetPlayer.IsDead) return;
            if (targetPlayer == null) return;

            double distanceToPlayer = CalculateDistanceToPlayer();
            UpdateFacingDirection();

            if (distanceToPlayer <= AttackRange)
            {
                CurrentState = EnemyState.Attacking;
                AttackPlayer(deltaTime);
            }
            else if (distanceToPlayer <= DetectionRange)
            {
                CurrentState = EnemyState.Chasing;
                ChasePlayer(deltaTime);
            }
            else
            {
                CurrentState = EnemyState.Idle;
                Velocity = new Vector(0, Velocity.Y);
            }

            UpdateAnimationState();
        }

        private double CalculateDistanceToPlayer()
        {
            return Math.Abs(Position.X - targetPlayer.Position.X);
        }

        private void UpdateFacingDirection()
        {
            FacingRight = targetPlayer.Position.X > Position.X;
        }

        private void ChasePlayer(double deltaTime)
        {
            Vector velocity = Velocity;
            int direction = FacingRight ? 1 : -1;
            velocity.X = direction * Speed;
            Velocity = velocity;
        }

        private void AttackPlayer(double deltaTime)
        {
            Velocity = new Vector(0, Velocity.Y);

            if (!isAttackInProgress && CanAttack())
            {
                // Начало атаки
                isAttackInProgress = true;
                attackStartTime = DateTime.Now;
                CurrentAnimation = animations["attack"];
                CurrentAnimation.Reset();
            }

            if (isAttackInProgress)
            {
                // Проверяем задержку перед уроном
                if ((DateTime.Now - attackStartTime).TotalSeconds >= AttackWindupTime)
                {
                    ApplyDamage();
                    isAttackInProgress = false;
                    lastAttackTime = DateTime.Now;
                }
            }
        }
        private void ApplyDamage()
        {
            // Наносим урон игроку
            targetPlayer.Health = Math.Max(0, targetPlayer.Health - (int)Damage);
            //soundManager.PlayEnemyAttack(); // звук атаки
        }
        private bool CanAttack()
        {
            return (DateTime.Now - lastAttackTime).TotalSeconds >= AttackCooldown;
        }

        private void UpdateAnimationState()
        {

            switch (CurrentState)
            {
                case EnemyState.Dead:
                    if (CurrentAnimation != animations["dead"])
                    {
                        CurrentAnimation = animations["dead"];
                        CurrentAnimation.Reset();
                    }
                    break;

                case EnemyState.Attacking:
                    if (CurrentAnimation != animations["attack"])
                    {
                        CurrentAnimation = animations["attack"];
                        CurrentAnimation.Reset();
                    }
                    break;
                case EnemyState.Chasing:
                    CurrentAnimation = animations["run"];
                    break;
                default:
                    CurrentAnimation = animations["idle"];
                    break;
            }
        }

        public void AddToCanvas(Canvas canvas)
        {
            canvas.Children.Add(image);
            canvas.Children.Add(HitboxVisual); //хитбокс отладки
        }
        public void RemoveFromCanvas(Canvas canvas)
        {
            canvas.Children.Remove(image);
            canvas.Children.Remove(HitboxVisual);
        }

        private void ApplyPhysics(double deltaTime)
        {
            var velocity = Velocity;
            velocity.Y += Gravity;
            Velocity = velocity;

            Position = new Point(
                Position.X + velocity.X,
                Position.Y + velocity.Y
            );
        }

        private void CheckCollisions(List<Platform> platforms, List<Entity> entities)
        {
            var enemyRect = Bounds;
            IsGrounded = false;

            // Коллизии с платформами
            foreach (var platform in platforms)
            {
                if (enemyRect.IntersectsWith(platform.Bounds))
                {
                    HandleCollision(platform.Bounds);
                }
            }

            // Коллизии с Entity
            foreach (var entity in entities)
            {
                if (entity.IsCollidable && enemyRect.IntersectsWith(entity.Bounds))
                {
                    HandleCollision(entity.Bounds);
                }
            }
        }
        private void HandleCollision(Rect obstacleRect)
        {
            var enemyRect = Bounds;
            double overlapX = Math.Min(enemyRect.Right, obstacleRect.Right) -
                            Math.Max(enemyRect.Left, obstacleRect.Left);
            double overlapY = Math.Min(enemyRect.Bottom, obstacleRect.Bottom) -
                            Math.Max(enemyRect.Top, obstacleRect.Top);

            if (overlapX > 0 && overlapY > 0)
            {
                if (overlapX < overlapY)
                {
                    // Горизонтальная коллизия
                    if (enemyRect.Left < obstacleRect.Left)
                    {
                        Position = new Point(Position.X - overlapX, Position.Y);
                    }
                    else
                    {
                        Position = new Point(Position.X + overlapX, Position.Y);
                    }
                    Velocity = new Vector(0, Velocity.Y);
                }
                else
                {
                    // Вертикальная коллизия
                    if (enemyRect.Top < obstacleRect.Top)
                    {
                        Position = new Point(Position.X, Position.Y - overlapY);
                        Velocity = new Vector(Velocity.X, 0);
                    }
                    else
                    {
                        Position = new Point(Position.X, Position.Y + overlapY);
                        Velocity = new Vector(Velocity.X, 0);
                        IsGrounded = true;
                    }
                }
            }
        }

        public void TakeDamage(int damage)
        {
            if (IsDead) return;

            PlayHitSound();

            Health = Math.Max(0, Health - damage);

            if (IsDead)
            {
                Die();
                ((MainWindow)Application.Current.MainWindow).player.AddExperience( ((Damage*AttackCooldown*2) + (Health/2) + (Speed*2))*4 );
            }
            else
            {
                // Анимация получения урона
            }
        }
        private void PlayHitSound()
        {
            try
            {
                if (_hitSoundPlayer == null)
                {
                    string soundPath = System.IO.Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "Assets",
                        "Player",
                        "hit.mp3"
                    );

                    _hitSoundPlayer = new MediaPlayer();
                    _hitSoundPlayer.Open(new Uri(soundPath, UriKind.Absolute));
                    _hitSoundPlayer.MediaEnded += (s, e) =>
                    {
                        _hitSoundPlayer.Stop();
                        _hitSoundPlayer.Position = TimeSpan.Zero;
                    };
                }

                _hitSoundPlayer.Position = TimeSpan.Zero;
                _hitSoundPlayer.Play();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка воспроизведения звука попадания: {ex.Message}");
            }
        }
        private void Die()
        {
            IsDeath = true;
            CurrentState = EnemyState.Dead;
            CurrentAnimation = animations["dead"];
            CurrentAnimation.IsLooping = false; // Отключаем зацикливание
            CurrentAnimation.Reset();
            Velocity = new Vector(0, 0);

            // Отключаем коллизии
            HitboxVisual.Visibility = Visibility.Collapsed;
        }
    }

    public class LevelExit
    {
        public Rect Bounds { get; }
        public Rectangle Visual { get; }

        public LevelExit(Rect bounds)
        {
            Bounds = bounds;
            Visual = new Rectangle
            {
                Width = bounds.Width,
                Height = bounds.Height,
                Fill = Brushes.Transparent,
                Stroke = Brushes.Cyan,
                StrokeThickness = 3,
                Visibility = Visibility.Collapsed
            };
            Canvas.SetZIndex(Visual, 999);
        }
    }
}