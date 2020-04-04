// An array of callbacks for when the page is loaded
var onLoadCallbacks = [];

// Listen out for page content loaded event
document.addEventListener("DOMContentLoaded", function()
{
    // Process anything that should happen once the page is loaded
    ProcessOnLoad();
});

// Helper function to iterate over arrays
function ForEach(array, callback)
{
    // Loop each item...
    for (var i = 0; i < array.length; i++)
    {
        // Pass the item back to the function
        callback(array[i]);
    }
}

// Process any on load events
function ProcessOnLoad()
{
    // Call the callback
    ForEach(onLoadCallbacks, function(item)
    {
        // Invoke the callback
        item();
    });
}

// Called to add a function to be invoked once the page loads
function OnLoad(callback)
{
    // Add this callback to the list
    onLoadCallbacks.push(callback);
}

// Generate a random number between the two values
function RandomNumber(from, to)
{
    return (Math.random() * (to - from)) + from;
}

// Universal request animation 
window.requestAnimationFrame = 
    window.requestAnimationFrame || 
    window.mozRequestAnimationFrame ||
    window.webkitRequestAnimationFrame ||
    window.msRequestAnimationFrame ||
    function(f) { setTimeout(f, 1000/60) };
    
// Get viewport height
function ViewportHeight()
{
    return Math.max(document.documentElement.clientHeight, window.innerHeight || 0);
}
    
// Get viewport width
function ViewportWidth()
{
    return Math.max(document.documentElement.clientWidth, window.innerWidth || 0);
}
