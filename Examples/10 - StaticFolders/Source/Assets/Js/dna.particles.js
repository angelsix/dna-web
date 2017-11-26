function ParticleBubbles()
{
    // Get all elements that want bubbles
    var bubbleElements = document.querySelectorAll("[data-particle-bubbles]");

    // For each of them
    ForEach(bubbleElements, function(element)
    {
        // Add number of bubbles based on a percentage of the width
        // Example: data-particle-bubbles="0.2", will add 20 bubbles to an element 100px wide
        var bubbleCount = element.offsetWidth * element.getAttribute("data-particle-bubbles");

        // Loop each bubble
        for (var i = 0; i < bubbleCount; i++)
        {
            // Size
            var size = RandomNumber(1,8);

            // Start between bottom 80%
            var top = RandomNumber(0, 80);

            // Anywhere along the width
            var left = RandomNumber(0, 100);
        
            // Randomly start animation at delayed time
            var delay = RandomNumber(1, 4);

            // Insert bubble
            element.innerHTML +=
                '<span class="bubble" style="top: ' + top +
                '%; left: ' + left +
                '%; width: ' + size +
                'px; height: ' + size +
                'px; animation-delay: ' + delay +
                's"></span>';
        }

    });
}

OnLoad(function()
{
    ParticleBubbles();
});