using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<TodoWorkDb>(opt => opt.UseInMemoryDatabase("ToDoWork"));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.MapGet("/todoworkitems", async (TodoWorkDb db) =>
    await db.Todos.ToListAsync());

app.MapGet("/todoworkitems/complete", async (TodoWorkDb db) =>
    await db.Todos.Where(t => t.IsComplete).ToListAsync());

app.MapGet("/todoworkitems/{id}", async (int id, TodoWorkDb db) =>
    await db.Todos.FindAsync(id)
        is TodoWork todo
            ? Results.Ok(todo)
            : Results.NotFound());

app.MapPost("/todoworkitems", async (TodoWork todo, TodoWorkDb db) =>
{
    db.Todos.Add(todo);
    await db.SaveChangesAsync();

    return Results.Created($"/todoitems/{todo.Id}", todo);
});

app.MapPut("/todoworkitems/{id}", async (int id, TodoWork inputTodo, TodoWorkDb db) =>
{
    var todo = await db.Todos.FindAsync(id);

    if (todo is null) return Results.NotFound();

    todo.Name = inputTodo.Name;
    todo.IsComplete = inputTodo.IsComplete;

    await db.SaveChangesAsync();

    return Results.NoContent();
});


app.MapDelete("/todoworkitems/{id}", async (int id, TodoWorkDb db) =>
{
    if (await db.Todos.FindAsync(id) is TodoWork todo)
    {
        db.Todos.Remove(todo);
        await db.SaveChangesAsync();
        return Results.Ok(todo);
    }

    return Results.NotFound();
});

app.Run();

class TodoWork
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public bool IsComplete { get; set; }
}

class TodoWorkDb : DbContext
{
    public TodoWorkDb(DbContextOptions<TodoWorkDb> options)
        : base(options) { }

    public DbSet<TodoWork> Todos => Set<TodoWork>();
}