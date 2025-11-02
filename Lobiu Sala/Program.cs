using System;
using System.Collections.Generic;
using System.Linq;

abstract class Entity
{
    public int HP { get; protected set; } = 100;
    public int X { get; protected set; }
    public int Y { get; protected set; }

    protected Entity(int hp, int x, int y)
    {
        HP = hp;
        X = x;
        Y = y;
    }

    public abstract char Symbol { get; }
    public virtual void Update(GameGrid grid) { }
}

class Player : Entity
{
    private readonly List<Used> _inventory = new();
    public bool HasTreasure { get; private set; } = false;
    public int Shields { get; private set; } = 0;
    public Action<string>? OnMessage;

    public Player(int x = 0, int y = 0) : base(100, x, y) { }

    public override char Symbol => 'P';

    public void CollectItem(Used item)
    {
        _inventory.Add(item);
        if (item is Item it && it.Name == "DIDYSIS LOBIS")
            HasTreasure = true;
        OnMessage?.Invoke($"Gavai: {(item as Item)?.Name}");
    }

    public void AddShields(int value = 1) => Shields += value;
    public void SetHP(int value) => HP = Math.Max(0, Math.Min(100, value));

    public void TakeDamage(int value)
    {
        if (Shields > 0)
        {
            Shields--;
            OnMessage?.Invoke("Skydas atmušė smūgį! Skydas sudužo.");
        }
        else
        {
            HP = Math.Max(0, HP - value);
            OnMessage?.Invoke($"-{value} HP! Liko HP: {HP}");
        }
    }

    public void PrintInv()
    {
        if (_inventory.Count == 0)
        {
            Console.WriteLine("Inventorius tuščias.");
            return;
        }

        var grouped = _inventory
            .OfType<Item>()
            .GroupBy(x => x.Name)
            .Select(g => new { Name = g.Key, Desc = g.First().Desc, Count = g.Count() })
            .OrderBy(x => x.Name);

        foreach (var item in grouped)
            Console.WriteLine($"x{item.Count} - {item.Name} - {item.Desc}");
    }

    public bool UseItem(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            OnMessage?.Invoke("Įveskite daikto pavadinimą.");
            return false;
        }

        string wanted = name.Trim();

        var usable = _inventory.FirstOrDefault(u =>
            u is Item it && it.Name.Equals(wanted, StringComparison.OrdinalIgnoreCase));

        if (usable == null)
        {
            OnMessage?.Invoke($"Neturite {wanted}");
            return false;
        }

        if (usable is Item it && it.Name.Equals("DIDYSIS LOBIS", StringComparison.OrdinalIgnoreCase))
        {
            OnMessage?.Invoke("„...Negalite panaudoti DIDŽIOJO LOBIO...");
            return false;
        }

        bool consumed = usable.Use(this);
        if (consumed) _inventory.Remove(usable);
        return consumed;
    }

    public void MoveBy(int dx, int dy, int MaxCols, int MaxRows)
    {
        int nx = X + dx;
        int ny = Y + dy;
        if (nx >= 0 && nx < MaxCols) X = nx;
        if (ny >= 0 && ny < MaxRows) Y = ny;
    }
}

interface Used { bool Use(Player p); }

class Item : Used
{
    public string Name { get; }
    public string Desc { get; }
    public bool Consumable { get; }
    private readonly Action<Player> _use;

    public Item(string name, string desc, bool consumable, Action<Player> useact)
    {
        Name = name;
        Desc = desc;
        Consumable = consumable;
        _use = useact ?? (_ => { });
    }

    public bool Use(Player p)
    {
        _use(p);
        return Consumable;
    }

    public static Item Water() => new Item("Vanduo", "Prideda +10 HP", true, p =>
    {
        int before = p.HP;
        p.SetHP(before + 10);
        p.OnMessage?.Invoke($"Išgėrei Vandenį (+{p.HP - before} HP).");
    });

    public static Item Bread() => new Item("Duona", "Prideda +20 HP", true, p =>
    {
        int before = p.HP;
        p.SetHP(before + 20);
        p.OnMessage?.Invoke($"Suvalgei Duoną (+{p.HP - before} HP).");
    });

    public static Item Shield() => new Item("Skydas", "Apgina nuo vieno sargybinio smūgio", true, p =>
    {
        p.AddShields(1);
        p.OnMessage?.Invoke("Gavai skydą! (Apgina nuo vieno smūgio)");
    });

    public static Item BigTreasure() => new Item(
        "DIDYSIS LOBIS",
        "Grąžinkite į startą, kad laimėtumėte!",
        false,
        p => p.OnMessage?.Invoke("Negalite naudoti šio lobio."));
}

class Enemy : Entity
{
    public string Name { get; }
    public int Damage { get; }

    public Enemy(string name, int damage, int x, int y) : base(1, x, y)
    {
        Name = name;
        Damage = damage;
    }

    public override char Symbol => 'E';
}

enum CellType { Empty, Item, Enemy, Treasure }

class Cell
{
    public CellType Type = CellType.Empty;
    public Item? PlacedItem;
    public Enemy? PlacedEnemy;
    public bool Consumed;
}

class GameGrid
{
    private readonly char[,] tiles;
    private readonly Cell[,] cells;

    public int Rows => tiles.GetLength(0);
    public int Columns => tiles.GetLength(1);

    public GameGrid(int rows, int cols, (int x, int y) startPos, int? seed)
    {
        tiles = new char[rows, cols];
        cells = new Cell[rows, cols];

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                tiles[r, c] = '#';
                cells[r, c] = new Cell();
            }

        Place(5, 8, startPos, seed);
    }

    public Cell GetCell(int r, int c) => cells[r, c];

    private void Place(int items, int enemies, (int x, int y) startPos, int? seed)
    {
        var rng = seed.HasValue ? new Random(seed.Value) : new Random();
        bool IsStart(int r, int c) => (c == startPos.x && r == startPos.y);

        // Treasure
        while (true)
        {
            int r = rng.Next(Rows);
            int c = rng.Next(Columns);
            if (!IsStart(r, c) && cells[r, c].Type == CellType.Empty)
            {
                cells[r, c].Type = CellType.Treasure;
                cells[r, c].PlacedItem = Item.BigTreasure();
                break;
            }
        }

        void PlaceItems(int count)
        {
            int placed = 0;
            while (placed < count)
            {
                int r = rng.Next(Rows);
                int c = rng.Next(Columns);
                if (!IsStart(r, c) && cells[r, c].Type == CellType.Empty)
                {
                    cells[r, c].Type = CellType.Item;
                    int roll = rng.Next(3);
                    cells[r, c].PlacedItem = roll switch
                    {
                        0 => Item.Water(),
                        1 => Item.Shield(),
                        _ => Item.Bread()
                    };
                    placed++;
                }
            }
        }

        void PlaceEnemies(int count)
        {
            int placed = 0;
            while (placed < count)
            {
                int r = rng.Next(Rows);
                int c = rng.Next(Columns);
                if (!IsStart(r, c) && cells[r, c].Type == CellType.Empty)
                {
                    cells[r, c].Type = CellType.Enemy;
                    cells[r, c].PlacedEnemy = new Enemy("SARGYBINIS", 15, c, r);
                    placed++;
                }
            }
        }

        PlaceItems(items);
        PlaceEnemies(enemies);
    }

    public void Print(IEnumerable<Entity> overlays)
    {
        var map = new Dictionary<(int r, int c), Entity>();
        foreach (var e in overlays) map[(e.Y, e.X)] = e;

        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Columns; c++)
            {
                if (map.TryGetValue((r, c), out var e))
                    Console.Write(e.Symbol + " ");
                else
                    Console.Write(tiles[r, c] + " ");
            }
            Console.WriteLine();
        }
    }
}

class Message
{
    public string Text { get; set; } = "";
    public DateTime ExpireAt { get; set; }
}

class Game
{
    private readonly GameGrid grid;
    private readonly Player player;
    private readonly (int x, int y) startPos;
    private readonly List<Entity> entities = new();
    private readonly List<Message> messages = new();

    public Game(int rows = 10, int cols = 10, int? seed = null)
    {
        startPos = (x: 0, y: 0);
        grid = new GameGrid(rows, cols, startPos, seed);
        player = new Player(startPos.x, startPos.y);
        player.OnMessage = AddMessage;
        entities.Add(player);
    }

    private void AddMessage(string text)
    {
        messages.Add(new Message { Text = text, ExpireAt = DateTime.Now.AddSeconds(2.5) });
        if (messages.Count > 6) messages.RemoveAt(0);
    }

    public void Run()
    {
        Console.CursorVisible = false;

        while (true)
        {
            Console.Clear();

            // pašalinam senas žinutes
            messages.RemoveAll(m => DateTime.Now > m.ExpireAt);

            grid.Print(entities);

            Console.WriteLine();
            Console.WriteLine($"HP: {player.HP}   Skydai: {player.Shields}");
            Console.WriteLine($"Pradžios taškas: ({startPos.x},{startPos.y})  Lobis: {(player.HasTreasure ? "TURITE" : "neturite")}");
            player.PrintInv();
            Console.WriteLine();

            // žinutės
            Console.WriteLine("---- Įvykiai ----");
            foreach (var msg in messages)
                Console.WriteLine(msg.Text);

            Console.WriteLine();
            Console.WriteLine("Judėjimas: WASD/rodyklės | U - naudoti daiktą | Q - išeiti");

            if (player.HasTreasure && player.X == startPos.x && player.Y == startPos.y)
            {
                CenterLine("Grąžinote LOBĮ į startą — JŪS LAIMĖJOTE!");
                Console.WriteLine("\nBet kuris klavišas...");
                Console.ReadKey(true);
                break;
            }

            var key = Console.ReadKey(true).Key;
            if (key == ConsoleKey.Q) break;

            if (key == ConsoleKey.U)
            {
                Console.Write("Ką naudoti? (duona, vanduo, skydas): ");
                var name = (Console.ReadLine() ?? "").Trim();
                if (!string.IsNullOrEmpty(name)) player.UseItem(name);
                continue;
            }

            int oldX = player.X, oldY = player.Y;
            MovePlayer(key);
            if (player.X != oldX || player.Y != oldY)
                ResolveTile(player.Y, player.X);
        }

        Console.CursorVisible = true;
    }

    private void MovePlayer(ConsoleKey key)
    {
        int dx = 0, dy = 0;
        switch (key)
        {
            case ConsoleKey.W:
            case ConsoleKey.UpArrow: dy = -1; break;
            case ConsoleKey.S:
            case ConsoleKey.DownArrow: dy = +1; break;
            case ConsoleKey.A:
            case ConsoleKey.LeftArrow: dx = -1; break;
            case ConsoleKey.D:
            case ConsoleKey.RightArrow: dx = +1; break;
            default: return;
        }

        player.MoveBy(dx, dy, grid.Columns, grid.Rows);
    }

    private void ResolveTile(int r, int c)
    {
        var cell = grid.GetCell(r, c);
        if (cell.Consumed) return;

        switch (cell.Type)
        {
            case CellType.Treasure:
                if (cell.PlacedItem != null)
                {
                    player.CollectItem(cell.PlacedItem);
                    AddMessage("Radote DIDĮJĮ LOBĮ! Grąžinkite jį į startą!");
                }
                cell.Consumed = true;
                break;

            case CellType.Item:
                if (cell.PlacedItem != null)
                    player.CollectItem(cell.PlacedItem);
                cell.Consumed = true;
                break;

            case CellType.Enemy:
                if (cell.PlacedEnemy != null)
                {
                    AddMessage($"Sutikote priešą: {cell.PlacedEnemy.Name}!");
                    player.TakeDamage(cell.PlacedEnemy.Damage);
                    if (player.HP <= 0)
                    {
                        Console.Clear();
                        CenterLine("Mirtinas smūgis. Žaidimas baigtas.");
                        Console.ReadKey(true);
                        Environment.Exit(0);
                    }
                }
                cell.Consumed = true;
                break;
        }
    }

    private static void CenterLine(string text)
    {
        int w = Console.WindowWidth;
        int pad = Math.Max(0, (w - text.Length) / 2);
        Console.WriteLine(new string(' ', pad) + text);
    }
}

class Program
{
    static void Main()
    {
        new Game(rows: 10, cols: 10).Run();
    }
}
