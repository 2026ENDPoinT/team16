using System;                                                                                                                                   
using System.Text;                                                                                                                              
using System.Threading.Tasks;                                                                                                                   
using UnityEngine;                                                                                                                              
using UnityEngine.Networking;                                                                                                                   
                                                                                                                                                
namespace Zzapmagochi.Net                                                                                                                       
{                                                                                                                                               
    /// <summary>                                                                                                                               
    /// FastAPI 로컬/실서버(기본 http://127.0.0.1:8000/v1)와 HTTP 통신하는 클라이언트.                                                          
    /// </summary>                                                                                                                              
    public class HttpApiClient : IApiClient                                                                                                     
    {                                                                                                                                           
        public string BaseUrl { get; set; }                                                                                                     
                                                                                                                                                
        [Serializable]                                                                                                                          
        private class ErrorEnvelope                                                                                                             
        {                                                                                                                                       
            public ErrorDetail error;                                                                                                           
        }                                                                                                                                       
                                                                                                                                                
        [Serializable]                                                                                                                          
        private class ErrorDetail                                                                                                               
        {                                                                                                                                       
            public string code;                                                                                                                 
            public string message;                                                                                                              
        }                                                                                                                                       
                                                                                                                                                
        public HttpApiClient(string baseUrl = "http://127.0.0.1:8000/v1")                                                                       
        {                                                                                                                                       
            BaseUrl = baseUrl.TrimEnd('/');                                                                                                     
        }                                                                                                                                       
                                                                                                                                                
        public async Task<AuthResponse> Login(string studentId, string password)                                                                
        {                                                                                                                                       
            var body = new LoginRequest                                                                                                         
            {                                                                                                                                   
                studentId = studentId,                                                                                                          
                password = password                                                                                                             
            };                                                                                                                                  
                                                                                                                                                
            var res = await SendJsonAsync<AuthResponse>(                                                                                        
                "POST",                                                                                                                         
                $"{BaseUrl}/auth/login",                                                                                                        
                JsonUtility.ToJson(body));                                                                                                      
                                                                                                                                                
            // 로그인 시에는 설문(scene_survey)을 건너뛰고 바로 scene_room으로 가도록 false 보장                                                
            res.isNewUser = false;                                                                                                              
            return res;                                                                                                                         
        }                                                                                                                                       
                                                                                                                                                
        public async Task<AuthResponse> SignUp(string studentId, string password)                                                               
        {                                                                                                                                       
            var body = new SignUpRequest                                                                                                        
            {                                                                                                                                   
                studentId = studentId,                                                                                                          
                password = password                                                                                                             
            };                                                                                                                                  
                                                                                                                                                
            var res = await SendJsonAsync<AuthResponse>(                                                                                        
                "POST",                                                                                                                         
                $"{BaseUrl}/auth/signup",                                                                                                       
                JsonUtility.ToJson(body));                                                                                                      
                                                                                                                                                
            // 회원가입 시에는 scene_survey(설문)를 거치도록 true 보장                                                                          
            res.isNewUser = true;                                                                                                               
            return res;                                                                                                                         
        }                                                                                                                                       
                                                                                                                                                
        public async Task<IdAvailabilityResponse> CheckStudentId(string studentId)                                                              
        {                                                                                                                                       
            var escaped = UnityWebRequest.EscapeURL(studentId ?? string.Empty);                                                                 
            var url = $"{BaseUrl}/auth/check-id?studentId={escaped}";                                                                           
            return await SendJsonAsync<IdAvailabilityResponse>("GET", url, null);                                                               
        }                                                                                                                                       
                                                                                                                                                
        public async Task<GitHubStreakPayload> LinkGitHub(string githubUsername, string oauthToken = null)                                      
        {                                                                                                                                       
            return await GitHubStreakGenerator.FetchPayloadAsync(githubUsername, oauthToken, "student");                                        
        }                                                                                                                                       
                                                                                                                                                
        public async Task<GitHubStreakPayload> GetGitHubStreak(string studentId)                                                                
        {                                                                                                                                       
            await Task.Yield();                                                                                                                 
            return GitHubStreakGenerator.CreateMockPayload(studentId, "zzap-coder");                                                            
        }                                                                                                                                       
                                                                                                                                                
        private static async Task<T> SendJsonAsync<T>(string method, string url, string jsonBody)                                               
        {                                                                                                                                       
            using var req = new UnityWebRequest(url, method);                                                                                   
            req.downloadHandler = new DownloadHandlerBuffer();                                                                                  
            req.timeout = 10;                                                                                                                   
                                                                                                                                                
            if (!string.IsNullOrEmpty(jsonBody))                                                                                                
            {                                                                                                                                   
                var raw = Encoding.UTF8.GetBytes(jsonBody);                                                                                     
                req.uploadHandler = new UploadHandlerRaw(raw);                                                                                  
                req.SetRequestHeader("Content-Type", "application/json");                                                                       
            }                                                                                                                                   
                                                                                                                                                
            req.SetRequestHeader("Accept", "application/json");                                                                                 
                                                                                                                                                
            var op = req.SendWebRequest();                                                                                                      
            while (!op.isDone)                                                                                                                  
                await Task.Yield();                                                                                                             
                                                                                                                                                
            var responseText = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;                                           
                                                                                                                                                
            if (req.result == UnityWebRequest.Result.ConnectionError ||                                                                         
                req.result == UnityWebRequest.Result.DataProcessingError)                                                                       
            {                                                                                                                                   
                throw new ApiException(0, ApiErrorCode.NetworkError, "서버에 연결할 수 없습니다.");                                             
            }                                                                                                                                   
                                                                                                                                                
            if (req.responseCode >= 400)                                                                                                        
            {                                                                                                                                   
                string code = ApiErrorCode.InvalidRequest;                                                                                      
                string message = "요청 처리에 실패했습니다.";                                                                                   
                                                                                                                                                
                if (!string.IsNullOrEmpty(responseText))                                                                                        
                {                                                                                                                               
                    try                                                                                                                         
                    {                                                                                                                           
                        var env = JsonUtility.FromJson<ErrorEnvelope>(responseText);                                                            
                        if (env != null && env.error != null)                                                                                   
                        {                                                                                                                       
                            if (!string.IsNullOrEmpty(env.error.code)) code = env.error.code;                                                   
                            if (!string.IsNullOrEmpty(env.error.message)) message = env.error.message;                                          
                        }                                                                                                                       
                    }                                                                                                                           
                    catch                                                                                                                       
                    {                                                                                                                           
                        // 파싱 실패 시 기본 메시지 유지                                                                                        
                    }                                                                                                                           
                }                                                                                                                               
                                                                                                                                                
                throw new ApiException(req.responseCode, code, message);                                                                        
            }                                                                                                                                   
                                                                                                                                                
            return JsonUtility.FromJson<T>(responseText);                                                                                       
        }                                                                                                                                       
    }                                                                                                                                           
}                                  
