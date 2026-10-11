using System;
using System.Collections.Generic;

namespace PuzzleOnline.Core
{
    /// <summary>
    /// Service Locator trung tâm cho toàn bộ dự án Puzzle Online.
    /// Cho phép Dependency Injection linh hoạt, hỗ trợ Unit Test / Mocking và loại bỏ sự phụ thuộc cứng vào Singleton.
    /// Tự động fallback về Instance mặc định nếu chưa được đăng ký thủ công (đảm bảo 100% backward compatibility).
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        /// <summary>
        /// Đăng ký triển khai cho một Interface dịch vụ.
        /// </summary>
        public static void Register<T>(T service) where T : class
        {
            if (service == null)
            {
                _services.Remove(typeof(T));
                return;
            }
            _services[typeof(T)] = service;
        }

        /// <summary>
        /// Hủy đăng ký dịch vụ.
        /// </summary>
        public static void Unregister<T>() where T : class
        {
            _services.Remove(typeof(T));
        }

        /// <summary>
        /// Lấy triển khai của dịch vụ T. Tự động fallback về Singleton mặc định nếu cần.
        /// </summary>
        public static T Get<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out var service) && service is T typedService)
            {
                return typedService;
            }

            // Fallback an toàn tới Singleton mặc định của Unity
            if (typeof(T) == typeof(IAudioService))
            {
                var mgr = Audio.AudioManager.Instance;
                if (mgr != null && mgr is T audioT)
                {
                    _services[typeof(T)] = audioT;
                    return audioT;
                }
            }
            else if (typeof(T) == typeof(INetworkService))
            {
                var net = Network.PuzzleNetworkManager.Instance;
                if (net != null && net is T netT)
                {
                    _services[typeof(T)] = netT;
                    return netT;
                }
            }
            else if (typeof(T) == typeof(IPuzzleCatalog))
            {
                var cat = PuzzleSystem.Gameplay.PuzzleCatalogManager.Instance;
                if (cat != null && cat is T catT)
                {
                    _services[typeof(T)] = catT;
                    return catT;
                }
                var reg = ContentRegistry.Instance;
                if (reg is T regT)
                {
                    _services[typeof(T)] = regT;
                    return regT;
                }
            }
            else if (typeof(T) == typeof(IThemeCatalog))
            {
                var themeCat = ContentRegistry.Instance;
                if (themeCat is T themeT)
                {
                    _services[typeof(T)] = themeT;
                    return themeT;
                }
            }
            else if (typeof(T) == typeof(IUIService))
            {
                var ui = UI.PuzzleOnlineUIManager.Instance;
                if (ui != null && ui is T uiT)
                {
                    _services[typeof(T)] = uiT;
                    return uiT;
                }
            }

            return null;
        }

        /// <summary>
        /// Xóa toàn bộ dịch vụ đã đăng ký (dùng cho Unit Test hoặc chuyển Scene).
        /// </summary>
        public static void Reset()
        {
            _services.Clear();
        }
    }
}
