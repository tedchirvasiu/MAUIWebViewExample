using System.Text.Json;
using System.Text.Json.Nodes;
using WebViewHostExample.ViewModels;

namespace WebViewHostExample;

public partial class MainPage : ContentPage
{
    int count = 0;
    MainPageViewModel vm;

    public class JavascriptAction {
        public string Type { get; set; }
        public JsonNode? Payload { get; set; }
        public string? ResponseToken { get; set; }
    }

	public MainPage()
	{
		InitializeComponent();

        vm = new MainPageViewModel();
        MyWebView.BindingContext = vm;

        MyWebView.JavaScriptAction += MyWebView_JavaScriptAction;
	}

    protected override void OnParentSet()
    {
        base.OnParentSet();
        vm.Source = new HtmlWebViewSource() { Html = htmlSource };
    }

    private async void MyWebView_JavaScriptAction(object sender, Controls.JavaScriptActionEventArgs e)
    {

        var action = JsonSerializer.Deserialize<JavascriptAction>(
            e.Payload,
            new JsonSerializerOptions() {
                PropertyNameCaseInsensitive = true
            }
        );

        try {

            string response = null;

            if (action.Type == "invoke_example") {

                Dispatcher.Dispatch(() =>
                {
                    ChangeLabel.Text = "The Web Button Was Clicked! Count: " + ((int)action.Payload?.AsValue());
                });

            } else if (action.Type == "call_example") {

                response = $"Hello from native code {count++}! Your message was: {(string)action.Payload?.AsValue()}.";
            }

            if (action.ResponseToken != null)
                await MyWebView.EvaluateJavaScriptAsync(new EvaluateJavaScriptAsyncRequest($"csharp.receiveResponse('{action.ResponseToken}', '{response.Replace("'", "\\'")}')"));

        } catch (Exception ex) {

            if (action.ResponseToken != null)
                await MyWebView.EvaluateJavaScriptAsync(new EvaluateJavaScriptAsyncRequest($"csharp.receiveError('{action.ResponseToken}')"));

            throw;
        }
    }

    string htmlSource = @"
<html>
<head></head>
<body>


<script>
var csharp = (function(){

    var responseMap = {};
    var responseIndex = 0;

    var invoke = function(action, data) {
        invokeCSharpAction(
            JSON.stringify({
                type: action,
                payload: data
            })
        );
    };

    var call = function(action, data) {
        
        var responseToken = (++responseIndex).toString();

        const responsePromise = new Promise((resolve, reject) => {
            responseMap[responseToken] = {
                resolve,
                reject
            };
        });

        invokeCSharpAction(
            JSON.stringify({
                type: action,
                payload: data,
                responseToken
            })
        );

        return responsePromise;
    };

    function receiveResponse(token, data) {
        responseMap[token].resolve(data);
        delete responseMap[token];
    }

    function receiveError(token) {
        responseMap[token].reject();
        delete responseMap[token];
    }
    
    return {
        invoke,
        call,
        receiveResponse,
        receiveError
    };
})();
</script>

<script>
    var counter = 1;
    function buttonClicked(e) {		
		csharp.invoke('invoke_example', counter++);
    }

    function nativeDemand(data) {
         var el = document.getElementById('webtext');
         el.innerHTML = data;
    }

    function asyncButtonClicked(e) {	

        var input = document.getElementById('client-message');

		csharp
            .call('call_example', input.value)
            .then(function(response) {
                document.getElementById('native-response').innerText = response;
            });
    }
</script>

<div style='display: flex; flex-direction: column; justify-content: center; align-items: center; width: 100%'>
<h2 style='font-family: script'><i>Fancy Web Title</i></h2>
<button style='height:48px; margin-left: 15px; margin-right: 15px; width: 128px; background: lightblue' id='hereBtn' onclick='javascript:buttonClicked(event)'>Click Me!</button>
<div id='webtext' style='font-family: script'><b>This web text will change when you push the native button.</b></div>

<h3>Bi-directional test</h3>
<input id='client-message' value='Blueberries' />
<button style='margin-top: 5px' onclick='javascript:asyncButtonClicked(event)'>Get a message from the native code!</button>
<div id='native-response'></div>
</div>
</html>
";

    private async void EvalButton_Clicked(object sender, EventArgs e)
    {
        await MyWebView.EvaluateJavaScriptAsync(new EvaluateJavaScriptAsyncRequest("nativeDemand('" + ChangeText.Text + "')")); 
    }
}

