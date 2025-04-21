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

namespace WpfGame
{
    public partial class MainWindow : Window
    {
        private SoundManager soundManager;
        private BackgroundManager backgroundManager;
        private Player player;
        private List<Entity> entities = new List<Entity>();
        private List<Platform> platforms = new List<Platform>();
        private List<ParallaxLayer> parallaxLayers = new List<ParallaxLayer>();
        private GameTimer gameTimer;
        private Level currentLevel;
        private List<Item> items = new List<Item>();
        private List<Enemy> enemies = new List<Enemy>();

        // настройки камеры
        private double CameraZoom = 2.0; // Увеличение в 2 раза
        private ScaleTransform cameraScaleTransform;
        // Камера
        private TranslateTransform cameraTransform;
        private const double CameraFollowSpeed = 0.1;
        private Rect levelBounds = new Rect(0, 0, 4000, 800); // Границы уровня

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                soundManager = new SoundManager();
                soundManager.PlayBackground();
                InitializeCamera();
                LoadGameAssets();
                InitializeGame();
                StartGameLoop();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing game: {ex.Message}");
                Close();
            }
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
            // Загрузка уровня
            string levelPath = "C:\\Users\\XOMA\\Documents\\GitHub\\projects-2024-2\\progr-sem-2\\Project2025-SS\\Levels\\level1.json";
            if (!File.Exists(levelPath))
            {
                throw new FileNotFoundException($"Level file not found: {levelPath}");
            }

            string levelJson = File.ReadAllText(levelPath);
            currentLevel = JsonConvert.DeserializeObject<Level>(levelJson)
                          ?? CreateDefaultLevel();

            // Инициализация параллакса
            parallaxLayers.Add(new ParallaxLayer("bg_layer9.png", 0));//стволы фон     005
            parallaxLayers.Add(new ParallaxLayer("bg_layer7.png", 0));//свет передний  015
            parallaxLayers.Add(new ParallaxLayer("bg_layer3.png", 0));//стволы задние   02
            parallaxLayers.Add(new ParallaxLayer("bg_layer6.png", 0));//свет задний     01
            parallaxLayers.Add(new ParallaxLayer("bg_layer2.png", 0));//стволы передние 05
            parallaxLayers.Add(new ParallaxLayer("bg_layer5.png", 0));//кроны          055
            parallaxLayers.Add(new ParallaxLayer("bg_layer1.png", 0));//трава задняя -0.8  08
            parallaxLayers.Add(new ParallaxLayer("bg_layer4.png", 0));//трава передняя  07


            // Загрузка спрайт-листов
            var idleSprite = LoadSprite("player_idle.png");
            var runSprite = LoadSprite("player_run.png");
            var jumpSprite = LoadSprite("player_jump.png");
            var attackSprite = LoadSprite("player_attack.png");

            // Инициализация анимаций
            var animations = new Dictionary<string, Animation>
            {
                {"idle", new Animation(idleSprite, 64, 80, 4, 0.1)},
                {"run", new Animation(runSprite, 80, 80, 8, 0.08)},
                {"jump", new Animation(jumpSprite, 80, 80, 8, 1.15)},
                {"attack", new Animation(attackSprite, 96, 80, 8, 0.07)}
            };

            player = new Player(new Point(640, 360 - 300), animations, soundManager, items, GameCanvas); // Смещение на 300px вверх

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
                //GameCanvas.Children.Add(platform.HitboxVisual); // Добавляем хитбокс
            }
            // Загрузка Entity
            if (currentLevel.Entities == null)
            {
                currentLevel.Entities = new List<EntityData>();
            }

            foreach (var entityData in currentLevel.Entities)
            {
                string texturePath = $"C:\\Users\\XOMA\\Documents\\GitHub\\projects-2024-2\\progr-sem-2\\Project2025-SS\\Assets\\Entities\\{entityData.Texture}";
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
                //GameCanvas.Children.Add(entity.HitboxVisual); // Добавляем хитбокс
            }

            // Загрузка врагов
            if (currentLevel.Enemies == null)
            {
                currentLevel.Enemies = new List<EnemyData>();
            }

            foreach (var enemyData in currentLevel.Enemies)
            {
                var animationse = new Dictionary<string, Animation>();
                foreach (var anim in enemyData.Animations)
                {
                    var sprite = LoadEnemySprite(anim.Value);
                    var animation = CreateEnemyAnimation(sprite, anim.Key);
                    animationse.Add(anim.Key.ToLower(), animation);
                }

                var enemy = new Enemy(
                    new Point(enemyData.X + enemyData.Width / 2, enemyData.Y + enemyData.Height / 2),
                    animationse,
                    "idle"
                );

                enemies.Add(enemy);
                enemy.AddToCanvas(GameCanvas);
            }
        }
        private BitmapImage LoadEnemySprite(string filename)
        {
            string fullPath = filename;
            return new BitmapImage(new Uri(fullPath));
        }

        private Animation CreateEnemyAnimation(BitmapImage sprite, string animationType)
        {
            switch (animationType.ToLower())
            {
                case "idle":
                    return new Animation(sprite, 112, 80, 5, 0.2);
                case "run":
                    return new Animation(sprite, 112, 80, 6, 0.1);
                case "attack":
                    return new Animation(sprite, 112, 80, 8, 0.07);
                default:
                    return new Animation(sprite, 112, 80, 1, 1.0);
            }
        }
        private Level CreateDefaultLevel()
        {
            return new Level
            {
                Platforms = new List<PlatformData>
            {
                new PlatformData { X = 0, Y = 1040, Width = 4000, Height = 120, Type = "ground" },
                //new PlatformData { X = 400, Y = 450, Width = 200, Height = 20, Type = "wood" },
                //new PlatformData { X = 800, Y = 350, Width = 150, Height = 20, Type = "stone" }
            }
            };
        }

        private BitmapImage LoadSprite(string filename)
        {
            string fullPath = $"C:\\Users\\XOMA\\Documents\\GitHub\\projects-2024-2\\progr-sem-2\\Project2025-SS\\Assets\\Player\\{filename}";
            return new BitmapImage(new Uri(fullPath));
        }

        private void InitializeGame()
        {
            backgroundManager = new BackgroundManager(GameCanvas);
            backgroundManager.AddLayer("C:\\Users\\XOMA\\Documents\\GitHub\\projects-2024-2\\progr-sem-2\\Project2025-SS\\Assets\\Backgrounds\\BG1\\bg_layer4.png", 0);
            backgroundManager.AddLayer("C:\\Users\\XOMA\\Documents\\GitHub\\projects-2024-2\\progr-sem-2\\Project2025-SS\\Assets\\Backgrounds\\BG1\\bg_layer1.png", 0);
            backgroundManager.AddLayer("C:\\Users\\XOMA\\Documents\\GitHub\\projects-2024-2\\progr-sem-2\\Project2025-SS\\Assets\\Backgrounds\\BG1\\bg_layer5.png", 0);//
            backgroundManager.AddLayer("C:\\Users\\XOMA\\Documents\\GitHub\\projects-2024-2\\progr-sem-2\\Project2025-SS\\Assets\\Backgrounds\\BG1\\bg_layer2.png", 0);//
            backgroundManager.AddLayer("C:\\Users\\XOMA\\Documents\\GitHub\\projects-2024-2\\progr-sem-2\\Project2025-SS\\Assets\\Backgrounds\\BG1\\bg_layer6.png", -0.1);//
            backgroundManager.AddLayer("C:\\Users\\XOMA\\Documents\\GitHub\\projects-2024-2\\progr-sem-2\\Project2025-SS\\Assets\\Backgrounds\\BG1\\bg_layer3.png", -0.2);//
            backgroundManager.AddLayer("C:\\Users\\XOMA\\Documents\\GitHub\\projects-2024-2\\progr-sem-2\\Project2025-SS\\Assets\\Backgrounds\\BG1\\bg_layer7.png", -0.2);//
            backgroundManager.AddLayer("C:\\Users\\XOMA\\Documents\\GitHub\\projects-2024-2\\progr-sem-2\\Project2025-SS\\Assets\\Backgrounds\\BG1\\bg_layer9.png", -0.4);//
            backgroundManager.AddLayer("C:\\Users\\XOMA\\Documents\\GitHub\\projects-2024-2\\progr-sem-2\\Project2025-SS\\Assets\\Backgrounds\\BG1\\bg_layer8.png", 0);

            gameTimer = new GameTimer();
            gameTimer.Update += GameUpdate;
            gameTimer.Render += GameRender;
        }

        private void StartGameLoop() => gameTimer.Start();

        private void GameUpdate(double deltaTime)
        {
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
                    //GameCanvas.Children.Remove(entities[i].HitboxVisual);
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
                    //GameCanvas.Children.Remove(item.HitboxVisual);
                }
                // Если подобран
                if (item.IsCollected)
                {
                    items.Remove(item);
                    GameCanvas.Children.Remove(item.Image);
                    //GameCanvas.Children.Remove(item.HitboxVisual);
                }
            }
            foreach (var enemy in enemies)
            {
                enemy.Update(deltaTime);
            }
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
            double healthPercent = player.Health / 100.0;
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
            double staminaPercent = player.Stamina / player.MaxStamina;
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
                //item.HitboxVisual.Stroke = Brushes.Lime;
                //item.HitboxVisual.StrokeThickness = 3;
                // Отладочный вывод позиций
                Console.WriteLine($"Item position: {item.Position.X}, {item.Position.Y}");
                //Console.WriteLine($"Canvas coords: {Canvas.GetLeft(item.HitboxVisual)}, {item.Position.X + cameraTransform.X}, {item.Position.Y + cameraTransform.Y}");

                item.UpdatePosition(cameraTransform.X, cameraTransform.Y);

                //Canvas.SetLeft(item.HitboxVisual, item.Position.X);
                //Canvas.SetTop(item.HitboxVisual, item.Position.Y);
            }
        }

        private void CheckInput()
        {
            player.MoveLeft = Keyboard.IsKeyDown(Key.A);
            player.MoveRight = Keyboard.IsKeyDown(Key.D);
            player.Jump = Keyboard.IsKeyDown(Key.Space);
            player.Attack = player.Attack = !player.staminaBlocked && Keyboard.IsKeyDown(Key.J);
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

            //масштабирование камеры
            if (e.Key == Key.Add)
                CameraZoom *= 1.1;
            if (e.Key == Key.Subtract)
                CameraZoom *= 0.9;

            cameraScaleTransform.ScaleX = CameraZoom;
            cameraScaleTransform.ScaleY = CameraZoom;
        }

        private void Window_KeyUp(object sender, KeyEventArgs e) { }
    }

    public class BackgroundManager
    {
        private readonly Canvas canvas;
        private readonly List<BackgroundLayer> layers = new List<BackgroundLayer>();

        public BackgroundManager(Canvas gameCanvas)
        {
            canvas = gameCanvas;
        }

        public void AddLayer(string imagePath, double speedFactor, int tileCount = 6) //tileCount - кол-во повторений текстуры
        {
            var layer = new BackgroundLayer(imagePath, speedFactor, tileCount);
            layers.Add(layer);

            foreach (var img in layer.Tiles)
            {
                canvas.Children.Insert(0, img);
            }
        }

        public void Update(double playerPositionX)
        {
            foreach (var layer in layers)
            {
                layer.Update(playerPositionX);
            }
        }
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
        public List<PlatformData> Platforms { get; set; } = new List<PlatformData>();
        public List<EntityData> Entities { get; set; } = new List<EntityData>();
        public List<EnemyData> Enemies { get; set; } = new List<EnemyData>();
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
        private SoundManager soundManager;
        private bool wasJumping;
        public Point Position { get; set; }
        public Size Size { get; } = new Size(30, 115); // Соответствует реальному размеру спрайта персонажа 128 160 70
        public int Health { get; set; } = 100; // здоровье
        public double Stamina { get; private set; } = 100; //выносливость
        public double MaxStamina { get; } = 100;
        private const double StaminaCost = 35; // Расход за атаку
        private const double StaminaRegen = 2; // Восстановление в секунду
        public bool staminaBlocked = false;
        public Item MainItem { get; private set; }
        public Item SecondaryItem { get; private set; }
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

        private DateTime lastAttackTime;
        private const double AttackCooldown = 0.5; // 0.5 секунд между атаками

        private readonly Dictionary<string, Animation> animations;
        private readonly List<Item> worldItems;
        private readonly Canvas gameCanvas;
        private const double MoveSpeed = 8;
        private const double JumpForce = -10;
        private const double Gravity = 0.8;

        public Player(Point startPosition, Dictionary<string, Animation> animations, SoundManager soundManager, List<Item> items, Canvas canvas)
        {
            Position = startPosition;
            this.animations = animations;
            CurrentAnimation = animations["idle"];
            this.soundManager = soundManager;
            this.worldItems = items;
            this.gameCanvas = canvas;
        }

        public void Update(double deltaTime, List<Platform> platforms, List<Entity> entities)
        {
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

            foreach (var entity in entities.ToArray())
            {
                if (entity.IsDestroyed ||
                    !entity.IsDestructible ||
                    !entity.Bounds.IntersectsWith(attackArea)) continue;

                entity.Destroy(worldItems, gameCanvas);
                entities.Remove(entity);
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
            var velocity = Velocity;
            velocity.X = 0;
            //MoveSpeed = 8 * _speedMultiplier;

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
            Velocity = velocity;
        }

        public void UseMainItem()
        {
            if (MainItem == null) return;

            // Определяем тип предмета по текстуре
            if (MainItem.Texture.UriSource.ToString().Contains("speed_potion.png"))
            {
                _speedMultiplier = 1.5;
                _speedBoostEndTime = DateTime.Now.AddMinutes(1);
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

        private Rect GetPlayerRect()
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
            soundManager.UpdateRunning(IsGrounded && Velocity.X != 0 && !Attack);
            soundManager.UpdateAttack(Attack);

            if (Attack)
            {
                //new SoundPlayer("C:\\Users\\XOMA\\Documents\\GitHub\\projects-2024-2\\progr-sem-2\\Project2025-SS\\Assets\\attack.wav").Play();
                CurrentAnimation = animations["attack"];

                //CurrentAnimation.Reset(); // Добавляем сброс анимации
            }
            else if (!IsGrounded)
            {
                CurrentAnimation = animations["jump"];
                //CurrentAnimation.Reset();
            }
            else if (Velocity.X != 0)
            {
                //new SoundPlayer("C:\\Users\\XOMA\\Documents\\GitHub\\projects-2024-2\\progr-sem-2\\Project2025-SS\\Assets\\run.wav").Play();
                CurrentAnimation = animations["run"];

            }
            else
            {
                CurrentAnimation = animations["idle"];
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

        private bool isRunning;
        private bool isAttacking;

        public SoundManager()
        {
            // Загрузка звуков
            jumpSound = new SoundPlayer("C:\\Users\\XOMA\\Documents\\GitHub\\projects-2024-2\\progr-sem-2\\Project2025-SS\\Assets\\Player\\jump.wav");
            runSound = new SoundPlayer("C:\\Users\\XOMA\\Documents\\GitHub\\projects-2024-2\\progr-sem-2\\Project2025-SS\\Assets\\Player\\run.wav");
            attackSound = new SoundPlayer("C:\\Users\\XOMA\\Documents\\GitHub\\projects-2024-2\\progr-sem-2\\Project2025-SS\\Assets\\Player\\attack.wav");

            potionSound = new SoundPlayer("C:\\Users\\XOMA\\Documents\\GitHub\\projects-2024-2\\progr-sem-2\\Project2025-SS\\Assets\\Player\\potion_sound.wav");
            swapSound = new SoundPlayer("C:\\Users\\XOMA\\Documents\\GitHub\\projects-2024-2\\progr-sem-2\\Project2025-SS\\Assets\\Player\\swap_sound.wav");

            // Настройка фоновой музыки
            backgroundPlayer.Open(new Uri("C:\\Users\\XOMA\\Documents\\GitHub\\projects-2024-2\\progr-sem-2\\Project2025-SS\\Assets\\Backgrounds\\BG1\\background.mp3", UriKind.Relative));
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

        public void Reset()
        {
            currentFrame = 0;
            currentTime = 0;
        }

        public Animation(BitmapSource sprite, int frameWidth, int frameHeight,
                        int framesCount, double frameDuration)
        {
            spriteSheet = sprite;
            this.frameWidth = frameWidth;
            this.frameHeight = frameHeight;
            this.framesCount = framesCount;
            this.frameDuration = frameDuration;

            // Добавим проверку размеров
            if (sprite.PixelWidth < frameWidth * framesCount)
            {
                throw new ArgumentException("Sprite sheet width is too small for specified frames");
            }
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
            currentTime += deltaTime;
            if (currentTime >= frameDuration)
            {
                currentFrame = (currentFrame + 1) % framesCount;
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
        //public Rectangle HitboxVisual { get; } // Хитбокс для отладки

        public Platform(Rect bounds, string type)
        {
            Bounds = bounds;
            Type = type;

            // Визуализация хитбокса
            //HitboxVisual = new Rectangle
            //{
            //    Width = bounds.Width,
            //    Height = bounds.Height,
            //    Stroke = Brushes.Blue,
            //    StrokeThickness = 2,
            //    Visibility = Visibility.Visible // Visible или Collapsed
            //};
            //Canvas.SetZIndex(HitboxVisual, 1000);
        }

        public void UpdatePosition(double cameraX)
        {
            //Canvas.SetLeft(HitboxVisual, Bounds.X);
            //Canvas.SetTop(HitboxVisual, Bounds.Y);
        }
    }

    public class Item
    {
        public Point Position { get; set; }
        public Vector Velocity { get; set; } = new Vector(0, 0);
        public Image Image { get; }
        //public Rectangle HitboxVisual { get; }
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

            //Canvas.SetLeft(HitboxVisual, Position.X);
            //Canvas.SetTop(HitboxVisual, Position.Y);
        }

        public Item(Point position, string texturePath)
        {
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

            //// Хитбокс для отладки
            //HitboxVisual = new Rectangle
            //{
            //    Width = 30,
            //    Height = 30,
            //    Stroke = Brushes.Green,
            //    StrokeThickness = 1,
            //    Visibility = Visibility.Visible
            //};
            Canvas.SetZIndex(Image, 200);
            //Canvas.SetZIndex(HitboxVisual, 200);
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
            //HitboxVisual.Visibility = Visibility.Collapsed;
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
    }

    public class Entity
    {
        public Rect Bounds { get; }
        public Image Image { get; }
        public bool IsDestructible { get; }
        public bool IsCollidable { get; }
        //public Rectangle HitboxVisual { get; } // Хитбокс для отладки
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
            //// Визуализация хитбокса
            //HitboxVisual = new Rectangle
            //{
            //    Width = bounds.Width,
            //    Height = bounds.Height,
            //    Stroke = isCollidable ? Brushes.Red : Brushes.Yellow,
            //    StrokeThickness = 2,
            //    Visibility = Visibility.Visible // Для отключения: Visibility.Collapsed
            //};
            //Canvas.SetZIndex(HitboxVisual, zIndex);
            Canvas.SetZIndex(Image, zIndex);

            // Загрузка звука
            if (!string.IsNullOrEmpty(destructionSound))
            {
                string soundPath = $"C:\\Users\\XOMA\\Documents\\GitHub\\projects-2024-2\\progr-sem-2\\Project2025-SS\\Assets\\Entities\\crate\\{destructionSound}";
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
            //HitboxVisual.Visibility = Visibility.Collapsed;
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

                        string texturePath = $"C:\\Users\\XOMA\\Documents\\GitHub\\projects-2024-2\\progr-sem-2\\Project2025-SS\\Assets\\Items\\{drop.Texture}";
                        var item = new Item(itemPos, texturePath);

                        items.Add(item);
                        canvas.Children.Add(item.Image);
                        //canvas.Children.Add(item.HitboxVisual);
                    }
                }
            }
        }

        public void UpdatePosition(double cameraX)
        {
            var transform = (TranslateTransform)Image.RenderTransform;
            transform.X = Bounds.X;
            transform.Y = Bounds.Y;

            //Canvas.SetLeft(HitboxVisual, Bounds.X);
            //Canvas.SetTop(HitboxVisual, Bounds.Y);
        }
    }

    public class ParallaxLayer
    {
        public BitmapImage Image { get; }
        public double SpeedFactor { get; }

        public ParallaxLayer(string imagePath, double speedFactor)
        {
            var uri = new Uri($"C:\\Users\\XOMA\\Documents\\GitHub\\projects-2024-2\\progr-sem-2\\Project2025-SS\\Assets\\Backgrounds\\BG1\\{imagePath}", UriKind.Absolute);
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

    public class EnemyData
    {
        public string Type { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public Dictionary<string, string> Animations { get; set; }
    }
    public class Enemy
    {
        public Point Position { get; set; }
        public Size Size { get; }
        public Animation CurrentAnimation { get; private set; }
        public bool FacingRight { get; set; }
        public Rect Bounds => new Rect(Position.X - Size.Width / 2, Position.Y - Size.Height / 2, Size.Width, Size.Height);

        private readonly Dictionary<string, Animation> animations;
        private readonly Image image;

        public Enemy(Point position, Dictionary<string, Animation> animations, string defaultAnimation)
        {
            Position = position;
            Size = new Size(224, 160); // Размеры скелета из JSON
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
        }

        public void Update(double deltaTime)
        {
            CurrentAnimation.Update(deltaTime);
            image.Source = CurrentAnimation.GetCurrentFrame(!FacingRight);

            // Обновление позиции на Canvas
            var transform = (TransformGroup)image.RenderTransform;
            var translate = (TranslateTransform)transform.Children[0];
            translate.X = Position.X - Size.Width / 2;
            translate.Y = Position.Y - Size.Height / 2;
        }

        public void AddToCanvas(Canvas canvas)
        {
            canvas.Children.Add(image);
        }
    }
}