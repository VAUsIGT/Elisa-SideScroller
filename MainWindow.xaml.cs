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
using System.Reflection.Emit;
using System.Media;

namespace WpfGame
{
    public partial class MainWindow : Window
    {
        
        private BackgroundManager backgroundManager;
        private Player player;
        private List<Platform> platforms = new List<Platform>();
        private List<ParallaxLayer> parallaxLayers = new List<ParallaxLayer>();
        private GameTimer gameTimer;
        private Level currentLevel;

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
            parallaxLayers.Add(new ParallaxLayer("bg_layer7.png", 0.15));//свет передний
            parallaxLayers.Add(new ParallaxLayer("bg_layer3.png", 0.2));//стволы задние
            parallaxLayers.Add(new ParallaxLayer("bg_layer6.png", 0.1));//свет задний
            parallaxLayers.Add(new ParallaxLayer("bg_layer2.png", 0.5));//стволы передние
            parallaxLayers.Add(new ParallaxLayer("bg_layer5.png", 0.55));//кроны
            parallaxLayers.Add(new ParallaxLayer("bg_layer1.png", 0.8));//трава задняя
            parallaxLayers.Add(new ParallaxLayer("bg_layer4.png", 0.7));//трава передняя
            

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
                {"attack", new Animation(attackSprite, 96, 80, 8, 0.1)}
            };

            player = new Player(new Point(640, 360 - 300), animations); // Смещение на 300px вверх

            // Создание платформ
            if (currentLevel.Platforms == null)
            {
                currentLevel.Platforms = new List<PlatformData>();
            }

            foreach (var platformData in currentLevel.Platforms)
            {
                platforms.Add(new Platform(
                    new Rect(platformData.X, platformData.Y, platformData.Width, platformData.Height),
                    platformData.Type
                ));
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
            string fullPath = $"C:\\Users\\XOMA\\Documents\\GitHub\\projects-2024-2\\progr-sem-2\\Project2025-SS\\Assets\\{filename}";
            return new BitmapImage(new Uri(fullPath));
        }

        private void InitializeGame()
        {
            backgroundManager = new BackgroundManager(GameCanvas);
            backgroundManager.AddLayer("C:\\Users\\XOMA\\Documents\\GitHub\\projects-2024-2\\progr-sem-2\\Project2025-SS\\Assets\\bg_layer4.png", 0.45);
            backgroundManager.AddLayer("C:\\Users\\XOMA\\Documents\\GitHub\\projects-2024-2\\progr-sem-2\\Project2025-SS\\Assets\\bg_layer1.png", 0.5);
            backgroundManager.AddLayer("C:\\Users\\XOMA\\Documents\\GitHub\\projects-2024-2\\progr-sem-2\\Project2025-SS\\Assets\\bg_layer5.png", 0.35);
            backgroundManager.AddLayer("C:\\Users\\XOMA\\Documents\\GitHub\\projects-2024-2\\progr-sem-2\\Project2025-SS\\Assets\\bg_layer2.png", 0.3);
            backgroundManager.AddLayer("C:\\Users\\XOMA\\Documents\\GitHub\\projects-2024-2\\progr-sem-2\\Project2025-SS\\Assets\\bg_layer6.png", 0.05);
            backgroundManager.AddLayer("C:\\Users\\XOMA\\Documents\\GitHub\\projects-2024-2\\progr-sem-2\\Project2025-SS\\Assets\\bg_layer3.png", 0.1);
            backgroundManager.AddLayer("C:\\Users\\XOMA\\Documents\\GitHub\\projects-2024-2\\progr-sem-2\\Project2025-SS\\Assets\\bg_layer7.png", 0.15);
            backgroundManager.AddLayer("C:\\Users\\XOMA\\Documents\\GitHub\\projects-2024-2\\progr-sem-2\\Project2025-SS\\Assets\\bg_layer8.png", 0);

            gameTimer = new GameTimer();
            gameTimer.Update += GameUpdate;
            gameTimer.Render += GameRender;
        }

        private void StartGameLoop() => gameTimer.Start();

        private void GameUpdate(double deltaTime)
        {
            player.Update(deltaTime, platforms);
            UpdateCamera();
            UpdateParallax();
            CheckInput();
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

            // Размеры спрайта из настроек персонажа
            double spriteWidth = player.Size.Width; // 128 из кода
            double spriteHeight = player.Size.Height-40; // 160

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
        }

        private void UpdateParallax()
        {
            backgroundManager.Update(cameraTransform.X);
        }

        private void CheckInput()
        {
            player.MoveLeft = Keyboard.IsKeyDown(Key.A);
            player.MoveRight = Keyboard.IsKeyDown(Key.D);
            player.Jump = Keyboard.IsKeyDown(Key.Space);
            player.Attack = Keyboard.IsKeyDown(Key.J);
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
            tileWidth = bitmap.PixelWidth+340;

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
        public List<PlatformData> Platforms { get; set; }
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
        public Point Position { get; set; }
        public Size Size { get; } = new Size(128, 160); // Соответствует реальному размеру спрайта персонажа
        public int Health { get; set; } = 100; // здоровье
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

        private readonly Dictionary<string, Animation> animations;
        private const double MoveSpeed = 4;
        private const double JumpForce = -10;
        private const double Gravity = 0.8;

        public Player(Point startPosition, Dictionary<string, Animation> animations)
        {
            Position = startPosition;
            this.animations = animations;
            CurrentAnimation = animations["idle"];
        }

        public void Update(double deltaTime, List<Platform> platforms)
        {
            HandleInput();
            ApplyPhysics(deltaTime);
            CheckCollisions(platforms);
            UpdateAnimationState();
            CurrentAnimation.Update(deltaTime);
        }

        private void HandleInput()
        {
            var velocity = Velocity;
            velocity.X = 0;

            if (MoveLeft)
            {
                velocity.X = -MoveSpeed;
                FacingRight = false;
            }
            if (MoveRight)
            {
                velocity.X = MoveSpeed;
                FacingRight = true;
            }
            if (Jump && IsGrounded)
            {
                velocity.Y = JumpForce;
                IsGrounded = false;
            }
            Velocity = velocity;
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

        private void CheckCollisions(List<Platform> platforms)
        {
            // В методе CheckCollisions()
            var playerRect = new Rect(
                Position.X - Size.Width / 2,
                Position.Y - Size.Height / 2,
                Size.Width,
                Size.Height
            );

            IsGrounded = false;

            foreach (var platform in platforms)
            {
                var platformRect = platform.Bounds;
                if (playerRect.IntersectsWith(platformRect))
                {
                    var velocity = Velocity;

                    // Вертикальные коллизии
                    var overlapY = playerRect.Bottom - platformRect.Top;
                    if (velocity.Y > 0 && overlapY > 0 && overlapY < 20)
                    {
                        Position = new Point(Position.X, platformRect.Top - Size.Height / 2);
                        velocity.Y = 0;
                        IsGrounded = true;
                    }
                    else if (velocity.Y < 0)
                    {
                        Position = new Point(Position.X, platformRect.Bottom + Size.Height / 2);
                        velocity.Y = 0;
                    }

                    // Горизонтальные коллизии
                    var overlapX = velocity.X > 0
                        ? playerRect.Right - platformRect.Left
                        : platformRect.Right - playerRect.Left;

                    if (Math.Abs(overlapX) < 20)
                    {
                        velocity.X = 0;
                    }

                    Velocity = velocity;
                }
            }
        }

        private void UpdateAnimationState()
        {
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

        public Platform(Rect bounds, string type)
        {
            Bounds = bounds;
            Type = type;
        }
    }

    public class ParallaxLayer
    {
        public BitmapImage Image { get; }
        public double SpeedFactor { get; }

        public ParallaxLayer(string imagePath, double speedFactor)
        {
            var uri = new Uri($"C:\\Users\\XOMA\\Documents\\GitHub\\projects-2024-2\\progr-sem-2\\Project2025-SS\\Assets\\{imagePath}", UriKind.Absolute);
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
}