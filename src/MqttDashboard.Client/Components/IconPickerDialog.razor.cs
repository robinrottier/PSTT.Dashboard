using MudBlazor;
using Microsoft.AspNetCore.Components;

namespace MqttDashboard.Components;

public partial class IconPickerDialog
{
    [CascadingParameter] private IMudDialogInstance? MudDialog { get; set; }
    [Parameter] public string? CurrentIcon { get; set; }

    private string? _selectedIcon;
    private string? _selectedIconName;
    private string _searchText = "";
    private List<IconCategory> _allCategories = new();

    public class IconCategory
    {
        public string Name { get; set; } = "";
        public string? IconPath { get; set; }
        public bool IsCategory { get; set; }
        public bool IsExpanded { get; set; } = true; // Default to expanded
        public HashSet<IconCategory> Children { get; set; } = new();
    }

    protected override void OnInitialized()
    {
        _selectedIcon = CurrentIcon;
        InitializeIconCategories();

        // Find and set the selected icon name if current icon is set
        if (!string.IsNullOrEmpty(CurrentIcon))
        {
            foreach (var category in _allCategories)
            {
                var icon = category.Children.FirstOrDefault(c => c.IconPath == CurrentIcon);
                if (icon != null)
                {
                    _selectedIconName = icon.Name;
                    break;
                }
            }
        }
    }

    private IEnumerable<IconCategory> FilteredCategories
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_searchText))
                return _allCategories;

            var filtered = new List<IconCategory>();
            var searchLower = _searchText.ToLower();

            foreach (var category in _allCategories)
            {
                var matchingChildren = category.Children
                    .Where(c => c.Name.ToLower().Contains(searchLower))
                    .ToHashSet();

                if (matchingChildren.Any())
                {
                    var filteredCategory = new IconCategory
                    {
                        Name = category.Name,
                        IsCategory = true,
                        IsExpanded = true,
                        Children = matchingChildren
                    };
                    filtered.Add(filteredCategory);
                }
            }

            return filtered;
        }
    }

    private void ToggleCategory(IconCategory category)
    {
        category.IsExpanded = !category.IsExpanded;
        StateHasChanged();
    }

    private void InitializeIconCategories()
    {
        _allCategories = new List<IconCategory>
        {
            new IconCategory
            {
                Name = "Household Appliances",
                IsCategory = true,
                Children = new HashSet<IconCategory>
                {
                    new IconCategory { Name = "Washing Machine", IconPath = Icons.Material.Filled.LocalLaundryService },
                    new IconCategory { Name = "Tumble Dryer", IconPath = Icons.Material.Filled.DryCleaning },
                    new IconCategory { Name = "Dishwasher", IconPath = Icons.Material.Filled.Countertops },
                    new IconCategory { Name = "Refrigerator", IconPath = Icons.Material.Filled.Kitchen },
                    new IconCategory { Name = "Freezer", IconPath = Icons.Material.Filled.AcUnit },
                    new IconCategory { Name = "Microwave", IconPath = Icons.Material.Filled.Microwave },
                    new IconCategory { Name = "Oven", IconPath = Icons.Material.Filled.OutdoorGrill },
                    new IconCategory { Name = "Cooker", IconPath = Icons.Material.Filled.SoupKitchen },
                    new IconCategory { Name = "Coffee Maker", IconPath = Icons.Material.Filled.Coffee },
                    new IconCategory { Name = "Blender", IconPath = Icons.Material.Filled.Blender },
                    new IconCategory { Name = "Kitchen", IconPath = Icons.Material.Filled.Restaurant },
                    new IconCategory { Name = "Vacuum", IconPath = Icons.Material.Filled.CleaningServices },
                    new IconCategory { Name = "Iron", IconPath = Icons.Material.Filled.Iron },
                }
            },
            new IconCategory
            {
                Name = "Technology",
                IsCategory = true,
                Children = new HashSet<IconCategory>
                {
                    new IconCategory { Name = "Computer", IconPath = Icons.Material.Filled.Computer },
                    new IconCategory { Name = "Laptop", IconPath = Icons.Material.Filled.LaptopMac },
                    new IconCategory { Name = "Phone", IconPath = Icons.Material.Filled.PhoneAndroid },
                    new IconCategory { Name = "Tablet", IconPath = Icons.Material.Filled.TabletMac },
                    new IconCategory { Name = "Watch", IconPath = Icons.Material.Filled.Watch },
                    new IconCategory { Name = "TV", IconPath = Icons.Material.Filled.Tv },
                    new IconCategory { Name = "Speaker", IconPath = Icons.Material.Filled.Speaker },
                    new IconCategory { Name = "Headphones", IconPath = Icons.Material.Filled.Headphones },
                    new IconCategory { Name = "Keyboard", IconPath = Icons.Material.Filled.Keyboard },
                    new IconCategory { Name = "Mouse", IconPath = Icons.Material.Filled.Mouse },
                    new IconCategory { Name = "Camera", IconPath = Icons.Material.Filled.Camera },
                    new IconCategory { Name = "Printer", IconPath = Icons.Material.Filled.Print },
                    new IconCategory { Name = "Scanner", IconPath = Icons.Material.Filled.Scanner },
                    new IconCategory { Name = "Router", IconPath = Icons.Material.Filled.Router },
                }
            },
            new IconCategory
            {
                Name = "Home & Building",
                IsCategory = true,
                Children = new HashSet<IconCategory>
                {
                    new IconCategory { Name = "Home", IconPath = Icons.Material.Filled.Home },
                    new IconCategory { Name = "House", IconPath = Icons.Material.Filled.House },
                    new IconCategory { Name = "Apartment", IconPath = Icons.Material.Filled.Apartment },
                    new IconCategory { Name = "Bed", IconPath = Icons.Material.Filled.Bed },
                    new IconCategory { Name = "Doorbell", IconPath = Icons.Material.Filled.Doorbell },
                    new IconCategory { Name = "Window", IconPath = Icons.Material.Filled.Window },
                    new IconCategory { Name = "Garage", IconPath = Icons.Material.Filled.Garage },
                    new IconCategory { Name = "Bathtub", IconPath = Icons.Material.Filled.Bathtub },
                    new IconCategory { Name = "Shower", IconPath = Icons.Material.Filled.Shower },
                    new IconCategory { Name = "Yard", IconPath = Icons.Material.Filled.Yard },
                }
            },
            new IconCategory
            {
                Name = "Energy & Climate",
                IsCategory = true,
                Children = new HashSet<IconCategory>
                {
                    new IconCategory { Name = "Light Bulb", IconPath = Icons.Material.Filled.Lightbulb },
                    new IconCategory { Name = "Solar Power", IconPath = Icons.Material.Filled.SolarPower },
                    new IconCategory { Name = "Battery", IconPath = Icons.Material.Filled.BatteryChargingFull },
                    new IconCategory { Name = "Power", IconPath = Icons.Material.Filled.Power },
                    new IconCategory { Name = "Thermostat", IconPath = Icons.Material.Filled.Thermostat },
                    new IconCategory { Name = "AC Unit", IconPath = Icons.Material.Filled.AcUnit },
                    new IconCategory { Name = "Heat", IconPath = Icons.Material.Filled.Whatshot },
                    new IconCategory { Name = "Water", IconPath = Icons.Material.Filled.WaterDrop },
                    new IconCategory { Name = "Gas", IconPath = Icons.Material.Filled.LocalGasStation },
                }
            },
            new IconCategory
            {
                Name = "Security & Safety",
                IsCategory = true,
                Children = new HashSet<IconCategory>
                {
                    new IconCategory { Name = "Lock", IconPath = Icons.Material.Filled.Lock },
                    new IconCategory { Name = "Key", IconPath = Icons.Material.Filled.Key },
                    new IconCategory { Name = "Security", IconPath = Icons.Material.Filled.Security },
                    new IconCategory { Name = "Camera Security", IconPath = Icons.Material.Filled.Videocam },
                    new IconCategory { Name = "Smoke Detector", IconPath = Icons.Material.Filled.Sensors },
                    new IconCategory { Name = "Fire Extinguisher", IconPath = Icons.Material.Filled.FireExtinguisher },
                    new IconCategory { Name = "Shield", IconPath = Icons.Material.Filled.Shield },
                    new IconCategory { Name = "Warning", IconPath = Icons.Material.Filled.Warning },
                }
            },
            new IconCategory
            {
                Name = "Network & IoT",
                IsCategory = true,
                Children = new HashSet<IconCategory>
                {
                    new IconCategory { Name = "Cloud", IconPath = Icons.Material.Filled.Cloud },
                    new IconCategory { Name = "WiFi", IconPath = Icons.Material.Filled.Wifi },
                    new IconCategory { Name = "Bluetooth", IconPath = Icons.Material.Filled.Bluetooth },
                    new IconCategory { Name = "Network", IconPath = Icons.Material.Filled.NetworkCheck },
                    new IconCategory { Name = "Hub", IconPath = Icons.Material.Filled.Hub },
                    new IconCategory { Name = "Sensors", IconPath = Icons.Material.Filled.Sensors },
                    new IconCategory { Name = "Settings Remote", IconPath = Icons.Material.Filled.SettingsRemote },
                    new IconCategory { Name = "Cable", IconPath = Icons.Material.Filled.Cable },
                }
            },
            new IconCategory
            {
                Name = "Storage & Organization",
                IsCategory = true,
                Children = new HashSet<IconCategory>
                {
                    new IconCategory { Name = "Storage", IconPath = Icons.Material.Filled.Storage },
                    new IconCategory { Name = "Archive", IconPath = Icons.Material.Filled.Archive },
                    new IconCategory { Name = "Folder", IconPath = Icons.Material.Filled.Folder },
                    new IconCategory { Name = "Inventory", IconPath = Icons.Material.Filled.Inventory },
                    new IconCategory { Name = "Category", IconPath = Icons.Material.Filled.Category },
                }
            },
            new IconCategory
            {
                Name = "Status & Indicators",
                IsCategory = true,
                Children = new HashSet<IconCategory>
                {
                    new IconCategory { Name = "Check Circle", IconPath = Icons.Material.Filled.CheckCircle },
                    new IconCategory { Name = "Error", IconPath = Icons.Material.Filled.Error },
                    new IconCategory { Name = "Info", IconPath = Icons.Material.Filled.Info },
                    new IconCategory { Name = "Star", IconPath = Icons.Material.Filled.Star },
                    new IconCategory { Name = "Favorite", IconPath = Icons.Material.Filled.Favorite },
                    new IconCategory { Name = "Bookmark", IconPath = Icons.Material.Filled.Bookmark },
                    new IconCategory { Name = "Flag", IconPath = Icons.Material.Filled.Flag },
                }
            },
            new IconCategory
            {
                Name = "Settings & Controls",
                IsCategory = true,
                Children = new HashSet<IconCategory>
                {
                    new IconCategory { Name = "Settings", IconPath = Icons.Material.Filled.Settings },
                    new IconCategory { Name = "Tune", IconPath = Icons.Material.Filled.Tune },
                    new IconCategory { Name = "Build", IconPath = Icons.Material.Filled.Build },
                    new IconCategory { Name = "Construction", IconPath = Icons.Material.Filled.Construction },
                    new IconCategory { Name = "Toggle On", IconPath = Icons.Material.Filled.ToggleOn },
                    new IconCategory { Name = "Toggle Off", IconPath = Icons.Material.Filled.ToggleOff },
                }
            }
        };
    }

    private void SelectIcon(string? iconPath, string iconName)
    {
        _selectedIcon = iconPath;
        _selectedIconName = iconName;
        StateHasChanged();
    }

    private void Select()
    {
        MudDialog?.Close(DialogResult.Ok(new { IconPath = _selectedIcon, IconName = _selectedIconName }));
    }

    private void Cancel()
    {
        MudDialog?.Cancel();
    }
}
