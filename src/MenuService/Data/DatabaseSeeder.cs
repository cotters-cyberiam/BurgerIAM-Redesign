namespace MenuService.Data;

public static class DatabaseSeeder
{
    public static void Seed(AppDbContext db)
    {
        if (db.MenuItems.Any()) return;

        db.MenuItems.AddRange(
            new MenuItemEntity { Name = "Classic Cheeseburger", Description = "Beef patty with cheddar, lettuce, tomato, and special sauce", Price = 5.99m, Category = "Burgers", IsAvailable = true, ImageUrl = "/images/classic-cheeseburger.jpg" },
            new MenuItemEntity { Name = "Double Bacon Burger", Description = "Two beef patties with crispy bacon, smoked gouda, and onion rings", Price = 8.49m, Category = "Burgers", IsAvailable = true, ImageUrl = "/images/double-bacon-burger.jpg" },
            new MenuItemEntity { Name = "Spicy Chicken Sandwich", Description = "Crispy chicken breast with jalapeños, pepper jack, and sriracha mayo", Price = 6.99m, Category = "Burgers", IsAvailable = true, ImageUrl = "/images/spicy-chicken-sandwich.jpg" },
            new MenuItemEntity { Name = "Veggie Deluxe", Description = "Plant-based patty with avocado, sprouts, and vegan aioli", Price = 7.49m, Category = "Burgers", IsAvailable = true, ImageUrl = "/images/veggie-deluxe.jpg" },
            new MenuItemEntity { Name = "Hand-Cut Fries", Description = "Crispy golden fries seasoned with sea salt and herbs", Price = 2.99m, Category = "Sides", IsAvailable = true, ImageUrl = "/images/hand-cut-fries.jpg" },
            new MenuItemEntity { Name = "Onion Rings", Description = "Beer-battered onion rings with chipotle dipping sauce", Price = 3.49m, Category = "Sides", IsAvailable = true, ImageUrl = "/images/onion-rings.jpg" },
            new MenuItemEntity { Name = "Coleslaw", Description = "Creamy classic coleslaw with a hint of apple cider vinegar", Price = 1.99m, Category = "Sides", IsAvailable = true, ImageUrl = "/images/coleslaw.jpg" },
            new MenuItemEntity { Name = "Chocolate Milkshake", Description = "Thick and creamy milkshake made with real Belgian chocolate", Price = 3.99m, Category = "Drinks", IsAvailable = true, ImageUrl = "/images/chocolate-milkshake.jpg" },
            new MenuItemEntity { Name = "Vanilla Milkshake", Description = "Classic vanilla milkshake with whipped cream and cherry", Price = 3.99m, Category = "Drinks", IsAvailable = true, ImageUrl = "/images/vanilla-milkshake.jpg" },
            new MenuItemEntity { Name = "Strawberry Shake", Description = "Fresh strawberry milkshake made with real fruit", Price = 4.49m, Category = "Drinks", IsAvailable = true, ImageUrl = "/images/strawberry-shake.jpg" },
            new MenuItemEntity { Name = "Iced Tea", Description = "Fresh-brewed iced tea served with lemon", Price = 1.99m, Category = "Drinks", IsAvailable = true, ImageUrl = "/images/iced-tea.jpg" },
            new MenuItemEntity { Name = "BBQ pulled Pork Sandwich", Description = "Slow-cooked pulled pork with tangy BBQ sauce and slaw", Price = 7.99m, Category = "Burgers", IsAvailable = true, ImageUrl = "/images/bbq-pulled-pork.jpg" },
            new MenuItemEntity { Name = "Mac and Cheese Bites", Description = "Crispy fried mac and cheese bites with ranch dip", Price = 4.49m, Category = "Sides", IsAvailable = true, ImageUrl = "/images/mac-cheese-bites.jpg" },
            new MenuItemEntity { Name = "Chicken Nuggets (6pc)", Description = "Crispy white-meat chicken nuggets with choice of sauce", Price = 4.99m, Category = "Sides", IsAvailable = true, ImageUrl = "/images/chicken-nuggets.jpg" }
        );

        db.SaveChanges();
    }
}
