// Callbacks to fire when the page scrolls
var scrollCallbacks = [];
var scrollInstantCallbacks = [];

// Tracking Id for the scroll timer
var scrollTimerCallbackId = null;

// The last time we scrolled
var lastScrollTime = 0;

// How often to update when scrolling
var scrollUpdateInterval = 100;

// Adds a function to be called whenever the page scrolls
function OnScroll(callback)
{
    // Add callback to list
    scrollCallbacks.push(callback);
}

// Adds a function to be called whenever the page scrolls
function OnScrollInstant(callback)
{
    // Add callback to list
    scrollInstantCallbacks.push(callback);
}

// Processes what should happen when the page scrolls
function ProcessOnScroll() 
{
    // Loop each callback
    ForEach(scrollCallbacks, function(item)
    {
        // Call the callback
        item();
    });
}

// Processes what should happen when the page scrolls
function ProcessOnScrollInstant() 
{
    // Loop each callback
    ForEach(scrollInstantCallbacks, function(item)
    {
        // Call the callback
        item();
    });
}

// Gets the scroll position top of the window
function ScrollPositionY()
{
    return window.ScrollY || window.scrollY || window.scrollTop || document.getElementsByTagName("html")[0].scrollTop;
}

// Hook into page scrolling
window.addEventListener("scroll", function()
{
    // Process
    requestAnimationFrame(ProcessOnScrollInstant);
                
    // If the last callback has finished 
    // or it has never been fired
    if (!scrollTimerCallbackId)
    {
        // If this is the first time we have scrolled
        if (lastScrollTime == 0)
        {
            // Update the last scroll time
            lastScrollTime = new Date().getTime();
     
            // Process
            requestAnimationFrame(ProcessOnScroll);
        }

        // Either way, start a timeout to fire after the interval
        scrollTimerCallbackId = setTimeout(function()
        {
            // Once timer is done, clear the Id so it can be called again
            scrollTimerCallbackId = null;

            // Update the last scroll time
            lastScrollTime = new Date().getTime();
            
            // Process
            requestAnimationFrame(ProcessOnScroll);

        }, scrollUpdateInterval);
    }
});