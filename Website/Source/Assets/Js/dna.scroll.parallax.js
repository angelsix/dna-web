OnLoad(function()
{
    // Find all elements with data-parallax-scroll-background attribute
    var selectedItems = document.querySelectorAll("[data-parallax-scroll-background]");

    // Fix all backgrounds to start at Y 0
    ForEach(selectedItems, function(element) { element.style.backgroundPositionY = "0px"; });

    // When we scroll...
    OnScrollInstant(function()
    {
        // For each element...
        ForEach(selectedItems, function(element) 
        { 
            // Get the element size
            var clientBounds = element.getBoundingClientRect();

            // Only nudge down background when it is in view
            if (clientBounds.bottom < 0)
                return;

            // Get parallax ratio
            var parallaxRatio = element.getAttribute("data-parallax-scroll-background");

            // Get bump down value
            var bumpY = clientBounds.top * parallaxRatio;

            // Apply it to the background position
            element.style.backgroundPositionY = bumpY + "px";
        });
    });
})