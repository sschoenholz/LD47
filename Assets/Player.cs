using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;

public enum WallColor
{
    Blue,
    Purple,
    Red,
    Orange,
    None,
    Count,
};

public struct Wall
{
    public Vector3Int start;
    public Vector3Int direction;
    public int length;
}

public class Player : MonoBehaviour
{
    public Grid background;
    public Grid holes;
    public Grid overlay;
    public bool solved;
    public string next_level;

    public TileBase filled_tile;

    public Sprite forward;
    public Sprite backward;
    public Sprite left;
    public Sprite right;

    public bool is_first;
    public bool is_last;

    public AudioSource walk_left;
    public AudioSource walk_right;
    public AudioSource sliding;
    public AudioSource falling;
    public AudioSource teleport;

    bool is_left_foot;

    Tilemap holemap;
    Tilemap tilemap;
    Tilemap backgroundmap;
    BoundsInt bounds;

    Vector3Int zero = new Vector3Int(0, 0, 0);
    Vector3Int dx = new Vector3Int(1, 0, 0);
    Vector3Int dy = new Vector3Int(0, 1, 0);

    SpriteRenderer sprite;

    Wall[] wall_a;
    bool[] wall_a_filled;

    Wall[] wall_b;
    bool[] wall_b_filled;

    Dictionary<Vector3Int, bool> wall_is_a;

    Dictionary<Vector3Int, Vector3Int> hole_map;

    // Start is called before the first frame update
    void Start()
    {
        backgroundmap = background.GetComponentInChildren<Tilemap>();
        tilemap = overlay.GetComponentInChildren<Tilemap>();
        if (holes != null)
            holemap = holes.GetComponentInChildren<Tilemap>();
        else
            holemap = tilemap;
        bounds = tilemap.cellBounds;

        wall_a = new Wall[(int)WallColor.Count];
        wall_a_filled = new bool[(int)WallColor.Count];

        wall_b = new Wall[(int)WallColor.Count];
        wall_b_filled = new bool[(int)WallColor.Count];

        wall_is_a = new Dictionary<Vector3Int, bool>();
        hole_map = new Dictionary<Vector3Int, Vector3Int>();

        solved = false;
        is_left_foot = false;

        sprite = GetComponent<SpriteRenderer>();

        if (!is_first)
            sprite.sprite = backward;

        identifyWalls();
        identifyHoles();
    }

    void identifyHoles()
    {
        Dictionary<string, Vector3Int> hole_a = new Dictionary<string, Vector3Int>();

        foreach (Vector3Int pos in bounds.allPositionsWithin)
        {
            TileBase tile = holemap.GetTile(pos);
            if (tile == null)
                continue;

            string[] name = tile.name.Split('_');

            if (name[0] == "hole")
            {
                if (!hole_a.ContainsKey(name[1]))
                    hole_a.Add(name[1], pos);
                else
                {
                    Vector3Int link_pos = hole_a[name[1]];
                    hole_map.Add(pos, link_pos);
                    hole_map.Add(link_pos, pos);
                }
            }
        }

        foreach (var k in hole_map.Keys)
        {
            TileBase maybe_box = tilemap.GetTile(hole_map[k]);

            if (maybe_box != null && maybe_box.name.Contains("box"))
                tilemap.SetTile(k, filled_tile);
        }
    }

    void identifyWalls()
    {
        HashSet<Vector3Int> tiles_visited = new HashSet<Vector3Int>();


        for (int i = 0; i < (int)WallColor.Count; i++)
            wall_a_filled[i] = false;

        foreach (Vector3Int pos in bounds.allPositionsWithin)
        {
            Vector3Int dir = zero;

            TileBase tile = tilemap.GetTile(pos);
            if (tile == null)
                continue;

            string name = tile.name;

            if (name.Contains("wall") && !tiles_visited.Contains(pos))
            {
                string[] arrow_name = tilemap.GetTile(pos).name.Split('_');
                string arrow_direction = arrow_name[arrow_name.Length - 1];

                if (arrow_direction == "up") dir = dy;
                if (arrow_direction == "down") dir = -dy;
                if (arrow_direction == "left") dir = -dx;
                if (arrow_direction == "right") dir = dx;

                WallColor color = getWallColor(pos);
                bool is_a = !wall_a_filled[(int)color];

                Wall wall = identifyWall(pos, dir, is_a, ref tiles_visited);

                if (is_a)
                {
                    wall_a[(int)color] = wall;
                    wall_a_filled[(int)color] = true;
                }
                else
                {
                    wall_b[(int)color] = wall;
                    wall_b_filled[(int)color] = true;
                }
            }
        }
    }

    Wall identifyWall(Vector3Int start, Vector3Int dir, bool is_a, ref HashSet<Vector3Int> visited)
    {
        Vector3Int end = start;

        visited.Add(start);
        wall_is_a[start] = is_a;

        while (isWall(end + dir))
        {
            end = end + dir;
            visited.Add(end);
            wall_is_a[end] = is_a;
        }

        while (isWall(start - dir))
        {
            start = start - dir;
            visited.Add(start);
            wall_is_a[start] = is_a;
        }

        Vector3Int dr = start - end;
        print(start);
        print(end);
        return new Wall() {
            start = start,
            direction = dir,
            length = (int)Mathf.Abs(dot(dr, dir)),
        }; 
    }

    bool isWall(Vector3Int pos)
    {
        if (!bounds.Contains(pos))
            return false;

        TileBase tile = tilemap.GetTile(pos);
        if (tile != null && tile.name.Contains("wall"))
            return true;

        return false;
    }

    WallColor getWallColor(Vector3Int pos)
    {
        if (tilemap.GetTile(pos).name.Contains("blue"))
            return WallColor.Blue;
        if (tilemap.GetTile(pos).name.Contains("red"))
            return WallColor.Red;
        if (tilemap.GetTile(pos).name.Contains("purple"))
            return WallColor.Purple;
        if (tilemap.GetTile(pos).name.Contains("orange"))
            return WallColor.Orange;

        return WallColor.None;
    }

    Vector3Int getWallOutDirection(Vector3Int pos)
    {
        string[] name = tilemap.GetTile(pos).name.Split('_');
        int len = name.Length - 1;

        int multiplier = -1;
        if (name[len - 1] == "bottom" || name[len - 1] == "left")
            multiplier = 1;

        if (name[len] == "left")
            return multiplier * dy;
        if (name[len] == "right")
            return multiplier * dy;
        if (name[len] == "up")
            return multiplier * dx;
        if (name[len] == "down")
            return multiplier * dx;

        return new Vector3Int(0, 0, 0);
    }

    (Vector3Int, Vector3Int) getWallOutPosition(Vector3Int pos)
    {
        WallColor color = getWallColor(pos);

        Wall sender = wall_is_a[pos] ? wall_a[(int)color] : wall_b[(int)color];
        Wall receiver = wall_is_a[pos] ? wall_b[(int)color] : wall_a[(int)color];

        int distance = dot(pos - sender.start, sender.direction);
        pos = receiver.start + distance * receiver.direction;
        Vector3Int dir = getWallOutDirection(pos);
        pos += dir;

        return (pos, dir);
    }

    bool isTraversable(Vector3Int point, Vector3Int dir)
    {
        if (!backgroundmap.cellBounds.Contains(point))
            return false;

        if (backgroundmap.GetTile(point) == null)
            return false;

        if (isWall(point)) 
            (point, dir) = getWallOutPosition(point);

        TileBase tile = tilemap.GetTile(point);
        if (tile != null)
        {
            if (tile.name.Contains("rock"))
                return false;
            if (tile.name.Contains("tilemap"))
                return false;
            if (is_first && tile.name.Contains("goal"))
                return false;
            if (tile.name.Contains("box"))
            {
                if (dir == zero)
                    return false;
                if (isPushable(point + dir, dir))
                    return true;
                return false;
            }
        }

        tile = holemap.GetTile(point);
        if (tile != null && tile.name.Contains("hole") && dir != zero)
        {
            if (hole_map.ContainsKey(point))
            {
                Vector3Int hole_point = hole_map[point];
                TileBase maybe_box = tilemap.GetTile(hole_point);
                if (maybe_box == null || !maybe_box.name.Contains("box"))
                    return isTraversable(hole_point + dir, dir);
            }
        }

        return true;
    }

    bool isPushable(Vector3Int point, Vector3Int dir)
    {
        if (!backgroundmap.cellBounds.Contains(point))
            return false;

        if (backgroundmap.GetTile(point) == null)
            return false;

        if (isWall(point))
            (point, dir) = getWallOutPosition(point);

        TileBase tile = tilemap.GetTile(point);
        if (tile != null)
        {
            if (tile.name.Contains("rock") || tile.name.Contains("goal"))
                return false;

            if (tile.name.Contains("tilemap"))
                return false;

            if (tile.name.Contains("box"))
            {
                if (isPushable(point + dir, dir))
                    return true;
                return false;
            }
        }

        return true;
    }

    Vector3Int move(Vector3Int pos, Vector3Int dir, bool is_falling=false)
    {
        bool play_walk = !is_falling;

        pos += dir;

        if (isWall(pos))
        {
            (pos, dir) = getWallOutPosition(pos);
            teleport.Play();
            play_walk = false;
        }

        TileBase tile = tilemap.GetTile(pos);
        if (tile != null && tile.name.Contains("box"))
            push(pos, dir);

        tile = holemap.GetTile(pos);
        if (tile != null && 
            tile.name.Contains("hole") && 
            hole_map.ContainsKey(pos) && 
            isTraversable(hole_map[pos], zero) && 
            isTraversable(hole_map[pos] + dir, dir))
        { 
            pos = move(hole_map[pos], dir, true);
            falling.Play();
            play_walk = false;
        }

        if (play_walk) {
            if (is_left_foot)
                walk_left.Play();
            else
                walk_right.Play();
            is_left_foot = !is_left_foot;
        }

        return pos;
    }

    void push(Vector3Int pos, Vector3Int dir)
    {
        sliding.Play();

        TileBase box_tile = tilemap.GetTile(pos);
        Vector3Int next_pos = pos + dir;

        if (isWall(next_pos))
            (next_pos, dir) = getWallOutPosition(next_pos);

        TileBase next_tile = tilemap.GetTile(next_pos);

        if (next_tile != null && next_tile.name.Contains("box"))
            push(next_pos, dir);

        TileBase hole_tile = holemap.GetTile(next_pos);
        if (hole_tile != null && hole_tile.name.Contains("hole") && hole_map.ContainsKey(next_pos))
            tilemap.SetTile(hole_map[next_pos], filled_tile);

        hole_tile = holemap.GetTile(pos);
        if (hole_tile != null && hole_tile.name.Contains("hole") && hole_map.ContainsKey(pos))
            tilemap.SetTile(hole_map[pos], null);

        tilemap.SetTile(next_pos, box_tile);
        tilemap.SetTile(pos, null);
    }

    int dot(Vector3Int a, Vector3Int b)
    {
        return a.x * b.x + a.y * b.y;
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 half = new Vector3(0.5f, 0.5f, 0.0f);

        Vector3Int pos = backgroundmap.WorldToCell(GetComponent<Transform>().position - half);

        if ((Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) && 
            isTraversable(pos + dy, dy))
        {
            sprite.sprite = forward;
            pos = move(pos, dy);
        }
        if ((Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) &&
            isTraversable(pos - dy, -dy))
        {
            sprite.sprite = backward;
            pos = move(pos, -dy);
        }
        if ((Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) &&
            isTraversable(pos - dx, -dx))
        {
            sprite.sprite = left;
            pos = move(pos, -dx);
        }
        if ((Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) && 
            isTraversable(pos + dx, dx))
        {
            sprite.sprite = right;
            pos = move(pos, dx);
        }
        if (Input.GetKeyDown(KeyCode.Escape))
            Application.Quit();

        if (Input.GetKeyDown(KeyCode.R))
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);

        TileBase tile = tilemap.GetTile(pos);
        bool is_first_or_last = is_first || is_last;
        if (tile != null &&
            ((tile.name.Contains("goal") && !is_first_or_last) ||
             (tile.name.Contains("bed") && is_first) ||
             (tile.name.Contains("hole") && is_last)))
        {
            // Do winning things.
            solved = true;
            SceneManager.LoadScene(next_level, LoadSceneMode.Single);
        }

        if (tile != null && tile.name.Contains("bed") && is_last)
            Application.Quit();

        this.transform.position = backgroundmap.CellToWorld(pos) + half;
    }
}
