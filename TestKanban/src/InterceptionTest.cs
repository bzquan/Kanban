using Unity;

namespace UnitTest
{
    [TestClass]
    public class InterceptionTest
    {
        IUnityContainer m_Container;
        InterceptionSUTInterface m_InterceptionSUT;

        [TestInitialize]
        public void Setup()
        {
            m_Container = new UnityContainer();
            //m_Container
            //    .AddNewExtension<Interception>()
            //    .RegisterType<InterceptionSUTInterface, InterceptionSUT>(
            //            new ContainerControlledLifetimeManager(),
            //            new Interceptor<InterfaceInterceptor>(),
            //            //new InterceptionBehavior<ExampleInterceptionBehavior>());
            //            new InterceptionBehavior<ExampleInterceptionBehaviorByUsingAsyncSupportExtension>());

            m_InterceptionSUT = m_Container.Resolve<InterceptionSUTInterface>();
        }

        [TestMethod]
        public void VoidMethod()
        {
            // Given
            int param = 0;

            // When
            m_InterceptionSUT.VoidMethod(out param);

            // Then
            Assert.AreEqual(10, param);
        }

        [TestMethod]
        public void Times2()
        {
            // Given
            int input = 10;
            int interceptionTimes = 2;

            // When
            int result = m_InterceptionSUT.Times2(input);

            // Then
            Assert.AreEqual(input * 2 * interceptionTimes, result);
        }

        [TestMethod]
        public void GenericMethodInt()
        {
            // Given

            // When
            bool result = m_InterceptionSUT.GenericMethod(10, 10);

            // Then
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void GenericMethodReturnDefaultInt()
        {
            // Given

            // When
            int result = m_InterceptionSUT.GenericMethodReturnDefault<int>();

            // Then
            Assert.AreEqual(0, result);
        }

        [TestMethod]
        public async Task AyncTemplateMethodReturnDefaultInt()
        {
            // Given

            // When
            int result = await m_InterceptionSUT.AyncTemplateMethodReturnDefault<int>();

            // Then
            Assert.AreEqual(0, result);
        }

        [TestMethod]
        public void GenericMethodReturnDefaultString()
        {
            // Given

            // When
            string result = m_InterceptionSUT.GenericMethodReturnDefault<string>();

            // Then
            Assert.AreEqual(null, result);
        }

        [TestMethod]
        public void GenericMethodString()
        {
            // Given

            // When
            bool result = m_InterceptionSUT.GenericMethod("a", "b");

            // Then
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task AyncMethod()
        {
            // Given
            int x = 10;

            // When
            int result = await m_InterceptionSUT.AyncMethod(x);

            // Then
            Assert.AreEqual(x * 2, result);
        }

        [TestMethod]
        public async Task AsyncGenericMethodInt()
        {
            // Given

            // When
            bool result = await m_InterceptionSUT.AyncTemplateMethod(10, 10);

            // Then
            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task AyncTemplateAddInt()
        {
            // Given

            // When
            int result = await m_InterceptionSUT.AyncTemplateAdd(10, 10);

            // Then
            Assert.AreEqual(20, result);
        }

        [TestMethod]
        public async Task AyncTemplateAddString()
        {
            // Given
            string first = "First";
            string second = "Second";

            // When
            string result = await m_InterceptionSUT.AyncTemplateAdd(first, second);

            // Then
            Assert.AreEqual(first + second, result);
        }
    }
}
