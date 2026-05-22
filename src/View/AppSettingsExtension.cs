using System;
using System.Configuration;
using System.Windows;
using System.Windows.Markup;

namespace Kanban
{
    /// <summary>
    /// 
    /// 本クラスは構成ファイル (.config) の <appSettings> の値を読み込むXAMLマークアップ拡張クラスです。
    /// 
    /// 例えば XAML でデータ バインディングをするときに、
    ///    Text="{Binding Message}" 
    /// のような記法を用いますが、これは XAML マークアップ拡張と呼ばれ、 
    /// Binding クラスが MarkupExtension クラスを継承しているためにこの形式の記述ができるようになっています。
    /// マークアップ拡張クラスでは、ProvideValue メソッドを実装して実際に設定する値を返します。
    /// 設定先となる依存関係プロパティを取得するには、
    /// ProvideValue メソッドに渡される serviceProvider から IProvideValueTarget を取り出します
    /// クラス名にサフィックスとして Extension を付けておくと、サフィックスを除いた名前で記述できます。
    /// 値を設定している部分は、
    ///    Height="{local:AppSettings Key=WindowHeight}"
    /// と書いてもよいですが、Key を引数に取るコンストラクターが存在することで、
    ///    Height="{local:AppSettings WindowHeight}"
    /// と書けるようになります。 
    /// なお、この部分は次のようにも書けます。
    ///    <Window> 
    ///       <Window.Height>
    ///          <local:AppSettings Key="WindowHeight"/> 
    ///       </Window.Height>
    ///    </Window> 
    ///          
    /// </summary>
    [MarkupExtensionReturnType(typeof(object))]
    public class AppSettingsExtension : MarkupExtension
    {
        public string Key { get; set; }
        
        public AppSettingsExtension()
        {
        }
        
        public AppSettingsExtension(string key)
        {
            Key = key;
        }
        
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));

            var type = GetTargetPropertyType(serviceProvider);
            if (type == null) return DependencyProperty.UnsetValue;

            var value = ConfigurationManager.AppSettings[Key];
            if (type.IsValueType && value == null) return DependencyProperty.UnsetValue;

            return type.IsEnum ?
                Enum.Parse(type, value) :
                Convert.ChangeType(value, type);
        }
        
        static Type GetTargetPropertyType(IServiceProvider serviceProvider)
        {
            var provider = serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
            if (provider == null) return null;

            var property = provider.TargetProperty as DependencyProperty;
            if (property == null) return null;

            return property.PropertyType;
        }
    }
}
