OnLoad(function()
{
    // Find all elements with attribute
    var items = document.querySelectorAll("[data-delay]");

    // For each element...
    ForEach(items, function(element) 
    { 
        // Wait the specified amount of time...
        setTimeout(function()
        {
            // Add desired classes
            element.classList.add(element.getAttribute("data-delay-add"));

            // Remove desired classes
            element.classList.remove(element.getAttribute("data-delay-remove"));
            
        }, element.getAttribute("data-delay") * 1000);
    });
})