
// The position at which to class as "not at the top"
var scrollDownDetectStart = 50;

// Wait for page to load...
OnLoad(function()
{
    // Find any elements with a data-scrolldown-class attribute
    // and adds that class to them when we scroll from the top of the the page
    var scrollDownItems = document.querySelectorAll("[data-scrolldown-class]");

    // Keep track of if we are scrolled down or up
    var scrolledDown = false;
    
    // Every time the page scrolls...
    OnScroll(function()
    {
        // Get scroll position
        var windowTop = ScrollPositionY();

        // If we have not scrolled down
        // and we are now scrolled down...
        if (!scrolledDown && windowTop > scrollDownDetectStart)
        {
            // Flag we have scrolled down
            scrolledDown = true;

            // For each element...
            ForEach(scrollDownItems, function(item)
            {
                // Add the desired class
                item.classList.add(item.getAttribute("data-scrolldown-class"));
            });
        }
        // Else if we scrolled back up...
        else if (scrolledDown && windowTop <= scrollDownDetectStart)
        {
            // Flag we are not scrolled down
            scrolledDown = false;
            
            // For each element...
            ForEach(scrollDownItems, function(item)
            {
                // Add the desired class
                item.classList.remove(item.getAttribute("data-scrolldown-class"));
            });                        
        }
    });
});