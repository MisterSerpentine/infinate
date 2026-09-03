using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A complete, asset-free 2D endless runner. Drop this script into any scene:
/// the RuntimeInitializeOnLoadMethod below creates the game automatically.
/// </summary>
public sealed class EndlessRunnerGame : MonoBehaviour
{
    private const float GroundY = -2.7f;
    private const float PlayerX = -4.1f;
    private const float PlayerWidth = 0.78f;
    private const float PlayerHeight = 1.05f;

    private static Sprite pixel;
    private static Sprite circle;

    private readonly List<Obstacle> obstacles = new();
    private readonly List<Transform> groundMarks = new();
    private readonly List<Transform> clouds = new();
    private readonly List<Transform> embers = new();

    private Transform player;
    private Transform body;
    private Transform frontLeg;
    private Transform backLeg;
    private Camera gameCamera;

    private float verticalVelocity;
    private float spawnTimer;
    private float elapsed;
    private float score;
    private float speed;
    private bool isGrounded;
    private bool gameOver;
    private int bestScore;

    private GUIStyle scoreStyle;
    private GUIStyle titleStyle;
    private GUIStyle hintStyle;
    private GUIStyle buttonStyle;

    private sealed class Obstacle
    {
        public Transform root;
        public float width;
        public float height;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<EndlessRunnerGame>() == null)
            new GameObject("Endless Runner").AddComponent<EndlessRunnerGame>();
    }

    private void Awake()
    {
        pixel = CreatePixelSprite();
        circle = CreateCircleSprite();
        bestScore = PlayerPrefs.GetInt("EndlessRunnerBest", 0);
        SetupCamera();
        BuildWorld();
        Restart();
    }

    private static Sprite CreatePixelSprite()
    {
        Texture2D texture = new(1, 1, TextureFormat.RGBA32, false)
        {
            name = "Runner Pixel",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    private static Sprite CreateCircleSprite()
    {
        const int size = 64;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            name = "Blood Moon",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        Color[] colors = new Color[size * size];
        Vector2 center = new((size - 1) * 0.5f, (size - 1) * 0.5f);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float distance = Vector2.Distance(new Vector2(x, y), center);
            float alpha = Mathf.Clamp01((size * 0.5f - distance) * 0.8f);
            colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
        }
        texture.SetPixels(colors);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private void SetupCamera()
    {
        gameCamera = Camera.main;
        if (gameCamera == null)
        {
            GameObject cameraObject = new("Main Camera");
            cameraObject.tag = "MainCamera";
            gameCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        gameCamera.orthographic = true;
        gameCamera.orthographicSize = 5f;
        gameCamera.transform.position = new Vector3(0f, 0f, -10f);
        gameCamera.backgroundColor = new Color(0.075f, 0.008f, 0.015f);
    }

    private void BuildWorld()
    {
        Transform skySymbol = new GameObject("Blood Moon Sigil").transform;
        skySymbol.position = new Vector3(4.8f, 2.2f, 0f);
        CreateBloodMoon(skySymbol);
        CreateLuciferSigil(skySymbol, 0.82f);

        // Jagged silhouettes make the horizon feel like the edge of a volcanic pit.
        for (int i = 0; i < 12; i++)
        {
            Transform peak = CreateBlock("Distant Peak", new Vector2(-9f + i * 1.7f, GroundY + 0.4f), new Vector2(1.45f, 2.4f + (i % 3) * 0.55f), new Color(0.12f, 0.018f, 0.028f), -3);
            peak.rotation = Quaternion.Euler(0f, 0f, 45f);
        }

        CreateBlock("Ground", new Vector2(0f, GroundY - 0.55f), new Vector2(30f, 1.1f), new Color(0.025f, 0.012f, 0.015f), -2);
        CreateBlock("Ground Line", new Vector2(0f, GroundY + 0.025f), new Vector2(30f, 0.065f), new Color(0.75f, 0.035f, 0.025f), 1);

        for (int i = 0; i < 13; i++)
        {
            Transform mark = CreateBlock("Burning Fissure", new Vector2(-8.5f + i * 1.55f, GroundY - 0.18f), new Vector2(0.62f, 0.055f), new Color(1f, 0.12f, 0.015f), 1);
            groundMarks.Add(mark);
        }

        for (int i = 0; i < 4; i++)
        {
            Transform cloud = new GameObject("Smoke").transform;
            cloud.position = new Vector3(-7f + i * 4.8f, 2.5f + (i % 2) * 0.65f, 0f);
            CreateBlock("Smoke A", Vector2.zero, new Vector2(1.15f, 0.28f), new Color(0.19f, 0.035f, 0.045f, 0.75f), -4, cloud);
            CreateBlock("Smoke B", new Vector2(-0.28f, 0.18f), new Vector2(0.5f, 0.38f), new Color(0.19f, 0.035f, 0.045f, 0.75f), -4, cloud);
            CreateBlock("Smoke C", new Vector2(0.2f, 0.22f), new Vector2(0.65f, 0.5f), new Color(0.19f, 0.035f, 0.045f, 0.75f), -4, cloud);
            clouds.Add(cloud);
        }

        for (int i = 0; i < 24; i++)
        {
            float emberSize = Random.Range(0.025f, 0.085f);
            Transform ember = CreateBlock("Ember", new Vector2(Random.Range(-9f, 9f), Random.Range(-2.3f, 4.7f)), new Vector2(emberSize, emberSize * 1.8f), new Color(1f, Random.Range(0.08f, 0.35f), 0.01f, 0.8f), 0);
            embers.Add(ember);
        }

        BuildPlayer();
    }

    private void BuildPlayer()
    {
        player = new GameObject("Runner").transform;
        Color demon = new(0.86f, 0.79f, 0.72f);
        body = CreateBlock("Demon Body", new Vector2(0f, 0.12f), new Vector2(0.78f, 0.72f), demon, 5, player);
        CreateBlock("Demon Head", new Vector2(0.25f, 0.48f), new Vector2(0.53f, 0.48f), demon, 5, player);
        CreateBlock("Snout", new Vector2(0.52f, 0.37f), new Vector2(0.27f, 0.2f), demon, 5, player);
        CreateBlock("Burning Eye", new Vector2(0.36f, 0.57f), new Vector2(0.11f, 0.075f), new Color(1f, 0.015f, 0.005f), 6, player);
        Transform leftHorn = CreateBlock("Left Horn", new Vector2(0.08f, 0.79f), new Vector2(0.15f, 0.42f), new Color(0.78f, 0.06f, 0.025f), 4, player);
        leftHorn.rotation = Quaternion.Euler(0f, 0f, -24f);
        Transform rightHorn = CreateBlock("Right Horn", new Vector2(0.38f, 0.78f), new Vector2(0.13f, 0.38f), new Color(0.78f, 0.06f, 0.025f), 4, player);
        rightHorn.rotation = Quaternion.Euler(0f, 0f, 24f);
        Transform tail = CreateBlock("Barbed Tail", new Vector2(-0.53f, 0.22f), new Vector2(0.44f, 0.12f), demon, 5, player);
        tail.rotation = Quaternion.Euler(0f, 0f, 28f);
        Transform barb = CreateBlock("Tail Barb", new Vector2(-0.73f, 0.36f), new Vector2(0.19f, 0.19f), new Color(0.78f, 0.06f, 0.025f), 6, player);
        barb.rotation = Quaternion.Euler(0f, 0f, 45f);
        backLeg = CreateBlock("Back Leg", new Vector2(-0.22f, -0.37f), new Vector2(0.18f, 0.43f), demon, 4, player);
        frontLeg = CreateBlock("Front Leg", new Vector2(0.23f, -0.37f), new Vector2(0.18f, 0.43f), demon, 5, player);
    }

    private void CreateBloodMoon(Transform parent)
    {
        GameObject moon = new("Blood Moon");
        moon.transform.SetParent(parent, false);
        moon.transform.localPosition = Vector3.zero;
        moon.transform.localScale = new Vector3(3.2f, 3.2f, 1f);
        SpriteRenderer renderer = moon.AddComponent<SpriteRenderer>();
        renderer.sprite = circle;
        renderer.color = new Color(0.48f, 0.015f, 0.025f, 0.86f);
        renderer.sortingOrder = -5;
    }

    private static void CreateLuciferSigil(Transform parent, float scale)
    {
        Transform symbol = new GameObject("Sigil of Lucifer").transform;
        symbol.SetParent(parent, false);
        symbol.localPosition = Vector3.zero;
        symbol.localScale = new Vector3(scale, scale, 1f);

        // The seal is split into strokes so its intersecting geometry stays crisp.
        CreateSigilStroke(symbol, new Vector2(-1f, 1.35f), new Vector2(1f, 1.35f));
        CreateSigilStroke(symbol, new Vector2(-1f, 1.35f), new Vector2(0.66f, -0.46f));
        CreateSigilStroke(symbol, new Vector2(-1f, 1.35f), new Vector2(0.22f, -0.98f));
        CreateSigilStroke(symbol, new Vector2(1f, 1.35f), new Vector2(-0.66f, -0.46f));
        CreateSigilStroke(symbol, new Vector2(1f, 1.35f), new Vector2(-0.22f, -0.98f));
        CreateSigilStroke(symbol, new Vector2(-0.32f, -0.62f), new Vector2(0f, -1.35f), new Vector2(0.32f, -0.62f));
        CreateSigilStroke(symbol, new Vector2(-0.45f, -0.62f), new Vector2(-0.22f, -0.62f));
        CreateSigilStroke(symbol, new Vector2(0.22f, -0.62f), new Vector2(0.45f, -0.62f));
        CreateSigilStroke(symbol, new Vector2(-0.3f, -0.63f), new Vector2(-0.52f, -0.7f), new Vector2(-0.68f, -0.88f), new Vector2(-0.58f, -1.03f), new Vector2(-0.42f, -0.94f));
        CreateSigilStroke(symbol, new Vector2(0.3f, -0.63f), new Vector2(0.52f, -0.7f), new Vector2(0.68f, -0.88f), new Vector2(0.58f, -1.03f), new Vector2(0.42f, -0.94f));
    }

    private static void CreateSigilStroke(Transform parent, params Vector2[] points)
    {
        GameObject stroke = new("Sigil Stroke");
        stroke.transform.SetParent(parent, false);
        LineRenderer line = stroke.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = false;
        line.positionCount = points.Length;
        line.startWidth = 0.035f;
        line.endWidth = 0.035f;
        line.startColor = new Color(1f, 0.08f, 0.025f, 0.72f);
        line.endColor = line.startColor;
        line.sortingOrder = -4;
        line.material = new Material(Shader.Find("Sprites/Default"));
        for (int i = 0; i < points.Length; i++)
            line.SetPosition(i, new Vector3(points[i].x, points[i].y, 0f));
    }

    private static Transform CreateBlock(string name, Vector2 position, Vector2 size, Color color, int order, Transform parent = null)
    {
        GameObject block = new(name);
        Transform transform = block.transform;
        transform.SetParent(parent, false);
        transform.localPosition = new Vector3(position.x, position.y, 0f);
        transform.localScale = new Vector3(size.x, size.y, 1f);
        SpriteRenderer renderer = block.AddComponent<SpriteRenderer>();
        renderer.sprite = pixel;
        renderer.color = color;
        renderer.sortingOrder = order;
        return transform;
    }

    private void Restart()
    {
        foreach (Obstacle obstacle in obstacles)
            if (obstacle.root != null) Destroy(obstacle.root.gameObject);
        obstacles.Clear();

        elapsed = 0f;
        score = 0f;
        speed = 6.3f;
        spawnTimer = 1.25f;
        verticalVelocity = 0f;
        isGrounded = true;
        gameOver = false;
        player.position = new Vector3(PlayerX, GroundY + PlayerHeight * 0.5f, 0f);
        player.rotation = Quaternion.identity;
    }

    private void Update()
    {
        bool jumpPressed = JumpPressed();
        if (gameOver)
        {
            if (jumpPressed) Restart();
            return;
        }

        elapsed += Time.deltaTime;
        score += Time.deltaTime * 10f;
        speed = Mathf.Min(12.5f, 6.3f + elapsed * 0.075f);

        if (jumpPressed && isGrounded)
        {
            verticalVelocity = 10.7f;
            isGrounded = false;
        }

        UpdatePlayer();
        UpdateScenery();
        UpdateObstacles();
    }

    private void UpdatePlayer()
    {
        verticalVelocity -= 28f * Time.deltaTime;
        Vector3 position = player.position;
        position.y += verticalVelocity * Time.deltaTime;
        float restingY = GroundY + PlayerHeight * 0.5f;
        if (position.y <= restingY)
        {
            position.y = restingY;
            verticalVelocity = 0f;
            isGrounded = true;
        }
        player.position = position;

        if (isGrounded)
        {
            float stride = Mathf.Sin(elapsed * speed * 2.6f) * 22f;
            frontLeg.localRotation = Quaternion.Euler(0f, 0f, stride);
            backLeg.localRotation = Quaternion.Euler(0f, 0f, -stride);
            body.localPosition = new Vector3(0f, 0.12f + Mathf.Abs(Mathf.Sin(elapsed * speed * 2.6f)) * 0.035f, 0f);
        }
        else
        {
            frontLeg.localRotation = Quaternion.Euler(0f, 0f, -18f);
            backLeg.localRotation = Quaternion.Euler(0f, 0f, 18f);
        }
    }

    private void UpdateScenery()
    {
        foreach (Transform mark in groundMarks)
        {
            mark.position += Vector3.left * speed * Time.deltaTime;
            if (mark.position.x < -9.5f)
                mark.position += Vector3.right * (groundMarks.Count * 1.55f);
        }

        foreach (Transform cloud in clouds)
        {
            cloud.position += Vector3.left * speed * 0.08f * Time.deltaTime;
            if (cloud.position.x < -10f)
                cloud.position += Vector3.right * 20f;
        }

        for (int i = 0; i < embers.Count; i++)
        {
            Transform ember = embers[i];
            ember.position += new Vector3(-0.08f, 0.42f + (i % 5) * 0.07f, 0f) * Time.deltaTime;
            if (ember.position.y > 5.2f)
                ember.position = new Vector3(Random.Range(-9f, 9f), -2.5f, 0f);
        }
    }

    private void UpdateObstacles()
    {
        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            SpawnObstacle();
            float minimumGap = Mathf.Lerp(1.15f, 0.82f, Mathf.InverseLerp(6.3f, 12.5f, speed));
            spawnTimer = Random.Range(minimumGap, minimumGap + 0.65f);
        }

        Rect playerRect = new(PlayerX - PlayerWidth * 0.38f, player.position.y - PlayerHeight * 0.46f, PlayerWidth * 0.76f, PlayerHeight * 0.88f);
        for (int i = obstacles.Count - 1; i >= 0; i--)
        {
            Obstacle obstacle = obstacles[i];
            obstacle.root.position += Vector3.left * speed * Time.deltaTime;
            Rect obstacleRect = new(obstacle.root.position.x - obstacle.width * 0.43f, GroundY, obstacle.width * 0.86f, obstacle.height * 0.92f);
            if (playerRect.Overlaps(obstacleRect))
            {
                EndGame();
                return;
            }

            if (obstacle.root.position.x < -10f)
            {
                Destroy(obstacle.root.gameObject);
                obstacles.RemoveAt(i);
            }
        }
    }

    private void SpawnObstacle()
    {
        float height = Random.Range(0.72f, 1.38f);
        float width = Random.Range(0.42f, 0.72f);
        Transform root = new GameObject("Obstacle").transform;
        root.position = new Vector3(10f, GroundY, 0f);

        Color basalt = new(0.34f, 0.08f, 0.075f);
        CreateBlock("Hell Pillar", new Vector2(0f, height * 0.5f), new Vector2(width * 0.78f, height), basalt, 3, root);
        CreateBlock("Glowing Rune", new Vector2(0f, height * 0.56f), new Vector2(width * 0.15f, height * 0.54f), new Color(1f, 0.28f, 0.015f), 4, root);
        Transform crownLeft = CreateBlock("Left Spike", new Vector2(-width * 0.23f, height + 0.02f), new Vector2(width * 0.25f, width * 0.55f), basalt, 3, root);
        crownLeft.rotation = Quaternion.Euler(0f, 0f, 35f);
        Transform crownRight = CreateBlock("Right Spike", new Vector2(width * 0.23f, height + 0.02f), new Vector2(width * 0.25f, width * 0.55f), basalt, 3, root);
        crownRight.rotation = Quaternion.Euler(0f, 0f, -35f);

        obstacles.Add(new Obstacle { root = root, width = width, height = height });
    }

    private void EndGame()
    {
        gameOver = true;
        player.rotation = Quaternion.Euler(0f, 0f, -72f);
        int finalScore = Mathf.FloorToInt(score);
        if (finalScore > bestScore)
        {
            bestScore = finalScore;
            PlayerPrefs.SetInt("EndlessRunnerBest", bestScore);
            PlayerPrefs.Save();
        }
    }

    private static bool JumpPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame || keyboard.rKey.wasPressedThisFrame))
            return true;
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            return true;
        return Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
    }

    private void OnGUI()
    {
        BuildStyles();
        float scale = Mathf.Clamp(Screen.height / 720f, 0.75f, 1.65f);
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale;
        float height = Screen.height / scale;

        GUI.Label(new Rect(24f, 18f, 400f, 48f), $"SOULS  {Mathf.FloorToInt(score):00000}", scoreStyle);
        GUI.Label(new Rect(width - 310f, 18f, 286f, 48f), $"DAMNED  {bestScore:00000}", scoreStyle);

        if (!gameOver && elapsed < 4.5f)
            GUI.Label(new Rect(0f, height * 0.18f, width, 45f), "SPACE / CLICK TO JUMP", hintStyle);

        if (gameOver)
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(0.05f, 0.07f, 0.08f, 0.72f);
            GUI.DrawTexture(new Rect(0f, 0f, width, height), Texture2D.whiteTexture);
            GUI.color = oldColor;
            GUI.Label(new Rect(0f, height * 0.28f, width, 72f), "SOUL CLAIMED", titleStyle);
            GUI.Label(new Rect(0f, height * 0.41f, width, 42f), $"SOULS  {Mathf.FloorToInt(score):00000}   •   DAMNED  {bestScore:00000}", hintStyle);
            if (GUI.Button(new Rect(width * 0.5f - 105f, height * 0.54f, 210f, 58f), "RUN AGAIN", buttonStyle))
                Restart();
            GUI.Label(new Rect(0f, height * 0.66f, width, 36f), "SPACE, R, CLICK OR TAP", hintStyle);
        }

        GUI.matrix = previousMatrix;
    }

    private void BuildStyles()
    {
        if (scoreStyle != null) return;
        scoreStyle = new GUIStyle(GUI.skin.label) { fontSize = 23, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        scoreStyle.normal.textColor = new Color(1f, 0.18f, 0.08f);
        titleStyle = new GUIStyle(scoreStyle) { fontSize = 48, alignment = TextAnchor.MiddleCenter };
        titleStyle.normal.textColor = Color.white;
        hintStyle = new GUIStyle(scoreStyle) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
        hintStyle.normal.textColor = Color.white;
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold };
        buttonStyle.normal.textColor = new Color(0.36f, 0.015f, 0.02f);
    }
}
