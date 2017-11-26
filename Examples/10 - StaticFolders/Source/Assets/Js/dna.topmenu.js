OnLoad(function()
{
    // Find all elements that have a href of #topmenu
    var menuExpanders = document.querySelectorAll("[href='#topmenu']");

    // Loop each menu expander
    ForEach(menuExpanders, function(item)
    {
        // Listen on the click event
        item.addEventListener("click", function(e)
        {
            // Cancel navigation
            e.preventDefault();

            // Add the desired class to all top menus
            var topMenus = document.querySelectorAll("[data-topmenu-class]");
            
            // For each menu...
            ForEach(topMenus, function(menu)
            {
                // Get the value of the data-topmenu-class attribute
                var menuClass = menu.getAttribute("data-topmenu-class");

                // Add/remove class
                menu.classList.toggle(menuClass);
            });
        });
    });
    
});