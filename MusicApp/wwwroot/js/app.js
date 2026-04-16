// Create a file named wwwroot/js/app.js

// Helper function to get all keys from localStorage that start with a prefix
window.localStorage.getKeys = function (prefix) {
    let keys = [];
    for (let i = 0; i < localStorage.length; i++) {
        const key = localStorage.key(i);
        if (key.startsWith(prefix)) {
            keys.push(key);
        }
    }
    return keys;
};

// Add audio player functionality
window.initAudioPlayer = function () {
    if (!document.getElementById('audioPreview')) {
        const audioElement = document.createElement('audio');
        audioElement.id = 'audioPreview';
        audioElement.addEventListener('ended', function() {
            window.dispatchEvent(new CustomEvent('previewEnded'));
        });
        document.body.appendChild(audioElement);
    }
};

window.playAudioPreview = function (url) {
    const audio = document.getElementById('audioPreview');
    if (audio) {
        audio.src = url;
        audio.play();
    }
};

window.stopAudioPreview = function () {
    const audio = document.getElementById('audioPreview');
    if (audio) {
        audio.pause();
        audio.currentTime = 0;
    }
};


// Add this script to your _Host.cshtml or import it in your project
window.initializeNetworkGraph = (svgElement, transformGroup, container, dotNetRef) => {
    if (!svgElement || !transformGroup || !container) return;
    
    const svg = svgElement;
    const g = transformGroup;
    
    // Set initial viewBox
    svg.setAttribute('viewBox', '-500 -500 1000 1000');
    
    // Initialize zoom and pan variables
    let zoom = 1;
    let panX = 0;
    let panY = 0;
    let startX = 0;
    let startY = 0;
    let isDragging = false;
    
    // Apply transform to the group
    function updateTransform() {
        g.setAttribute('transform', `translate(${panX}, ${panY}) scale(${zoom})`);
    }
    
    // Handle wheel event for zooming
    function handleWheel(event) {
        event.preventDefault();
        
        const delta = event.deltaY;
        const scaleAmount = delta > 0 ? 0.9 : 1.1; // Zoom in or out
        
        // Calculate new zoom level with limits
        const newZoom = Math.max(0.1, Math.min(5, zoom * scaleAmount));
        
        // Adjust pan to zoom towards mouse position
        const rect = svg.getBoundingClientRect();
        const mouseX = event.clientX - rect.left;
        const mouseY = event.clientY - rect.top;
        
        // Convert mouse position to SVG coordinates
        const svgX = (mouseX / rect.width) * 1000 - 500;
        const svgY = (mouseY / rect.height) * 1000 - 500;
        
        // Adjust pan to zoom towards mouse position
        panX = panX + (svgX - panX) * (1 - scaleAmount);
        panY = panY + (svgY - panY) * (1 - scaleAmount);
        
        zoom = newZoom;
        
        // Update the transform
        updateTransform();
        
        // Notify .NET component of the change
        dotNetRef.invokeMethodAsync('UpdatePanZoom', zoom, panX, panY);
    }
    
    // Handle mouse down for panning
    function handleMouseDown(event) {
        if (event.button === 0) { // Left mouse button
            isDragging = true;
            container.classList.add('grabbing');
            
            // Store initial position
            startX = event.clientX;
            startY = event.clientY;
            
            // Notify .NET component
            dotNetRef.invokeMethodAsync('OnGraphDragStart');
            
            event.preventDefault();
        }
    }
    
    // Handle mouse move for panning
    function handleMouseMove(event) {
        if (!isDragging) return;
        
        // Calculate how much the mouse has moved
        const dx = event.clientX - startX;
        const dy = event.clientY - startY;
        
        // Calculate the equivalent change in SVG coordinates
        const rect = svg.getBoundingClientRect();
        const svgDx = (dx / rect.width) * 1000;
        const svgDy = (dy / rect.height) * 1000;
        
        // Update pan values
        panX += svgDx;
        panY += svgDy;
        
        // Update start position for next move
        startX = event.clientX;
        startY = event.clientY;
        
        // Update the transform
        updateTransform();
        
        event.preventDefault();
    }
    
    // Handle mouse up to end panning
    function handleMouseUp(event) {
        if (isDragging) {
            isDragging = false;
            container.classList.remove('grabbing');
            
            // Notify .NET component
            dotNetRef.invokeMethodAsync('OnGraphDragEnd');
            
            event.preventDefault();
        }
    }
    
    // Handle tooltip position updates
    function updateTooltipPosition(event) {
        // This would be implemented if we want to position tooltips via JS
        // For now, we're handling tooltips directly in the .NET component
    }
    
    // Add event listeners
    svg.addEventListener('wheel', handleWheel, { passive: false });
    svg.addEventListener('mousedown', handleMouseDown);
    document.addEventListener('mousemove', handleMouseMove);
    document.addEventListener('mouseup', handleMouseUp);
    
    // Expose zoom functions to be called from .NET
    window.zoomNetworkGraph = (factor) => {
        zoom = Math.max(0.1, Math.min(5, zoom * factor));
        updateTransform();
        dotNetRef.invokeMethodAsync('UpdatePanZoom', zoom, panX, panY);
    };
    
    window.resetNetworkGraph = () => {
        zoom = 1;
        panX = 0;
        panY = 0;
        updateTransform();
        dotNetRef.invokeMethodAsync('UpdatePanZoom', zoom, panX, panY);
    };
    
    // Clean up function to remove event listeners (to be called when component is disposed)
    return () => {
        svg.removeEventListener('wheel', handleWheel);
        svg.removeEventListener('mousedown', handleMouseDown);
        document.removeEventListener('mousemove', handleMouseMove);
        document.removeEventListener('mouseup', handleMouseUp);
    };
};