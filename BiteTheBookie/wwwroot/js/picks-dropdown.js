document.addEventListener('DOMContentLoaded', function () {
    const dropdown = document.querySelector('.picks-dropdown');
    
    if (dropdown !== null && window.innerWidth > 991) {
        // Desktop: Show on hover
        dropdown.addEventListener('mouseenter', function () {
            const menu = this.querySelector('.dropdown-menu');
            if (menu) {
            menu.classList.add('show');
            }
        });

        dropdown.addEventListener('mouseleave', function () {
            const menu = this.querySelector('.dropdown-menu');
            if (menu) {
            menu.classList.remove('show');
            }
        });
    }
    
    // Track dropdown usage
    const dropdownItems = document.querySelectorAll('.picks-dropdown .dropdown-item');
    dropdownItems.forEach(item => {
        if (item) {
            item.addEventListener('click', function () {
                console.log('Picks navigation:', this.textContent.trim());
            });
        }
    });
});