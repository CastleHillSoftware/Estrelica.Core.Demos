using System;
using System.Net.Http;
using Estrelica;

namespace Snippets
{
	class Program
	{
		static void Main(string[] args)
		{
			Console.WriteLine("This project is not a functional application, but instead just provides general code snippets showing how to do " +
				"various things with Estrelica.Core that don't fit into the functional demos.");
		}

		static void DisableSSLCertificateVerification()
		{
			/* Sometimes (such as when using an Archer development instance with a self-signed certificate) it is necessary to disable the verification of SSL certificates, otherwise
			 * exceptions will occur when any Archer API calls are attempted.  This can be achieved by setting the static DisableSSLCertificateVerification property to true at the 
			 * very beginning of your application, before authenticating or instantiating any Estrelica.Core objects.  Note also that this property may only be set once, and cannot
			 * be switched back off again during your process's execution: */

			Estrelica.Core.DisableSSLCertificateVerification = true;

			// then proceed to authenticate and invoke an instance as usual...

			var core = CoreConfig.Load(w => Console.WriteLine(w.Message));
		}

		static void UseCustomWebProxy()
		{
			/* Estrelica.Core supports custom Proxy settings for situations where the default proxy settings on the local machine do not work.  You can easily instantiate
			 * a new System.Net.WebProxy object with whatever custom settings you require, and direct Estrelica.Core to use that for all of its API calls.  For example, 
			 * to make Estrelica.Core ignore the system-defined proxy for local addresses, just do this: */
			Estrelica.Core.Proxy = new System.Net.WebProxy { BypassProxyOnLocal = true };
		}

		static void UseWebservicesForAuthentication()
		{
			/* Estrelica.Core defaults to using the newer REST API vs. the older SOAP/Webservices API when functionality is duplicated between the two.  One exception to this is
			 * authentication.  When establishing a new session Estrelica.Core will, by default, use the SOAP /ws/general.asmx CreateDomainUserSessionFromInstance and 
			 * CreateUserSessionFromInstance methods instead of the REST platformapi/core/security/login endpoint.  This is because the REST login method does not work in
			 * environments configured for SSO with manual logins disabled, while the SOAP authentication method is unaffected by that configuration.
			 * If, however, you want to override this behavior so that Estrelica.Core always authenticates via the REST API, you can control via the static Estrelica.Core.UseRESTLogin 
			 * property.  Since authentication is one of the first things that Estrelica.Core does on startup, this must be set before instantiating any Estrelica.Core objects: */

			Estrelica.Core.UseRESTLogin = true;

			// then proceed to authenticate and invoke an instance as usual...

			var core = CoreConfig.Load(w => Console.WriteLine(w.Message));
		}

		static void CustomLicenseLocation()
		{
			/* By default, Estrelica.Core stores its license information in a text file (named Estrelica.Core.license) located in your process's execution directory.
			 * If you'd like this license to be stored elsewhere, you can specify a different directory via the licenseFileLocation string parameter on both the
			 * Estrelica.Core.ValidateLicense() method (if you're handling your own authentication): */

			var core1 = Estrelica.Core.ValidateLicense(new Guid("xxxxx"), w => Console.WriteLine(w.Message), licenseFileLocation: @"c:\temp");

			/* and on the CoreConfig.Load method (if you're using a standard appSettings file): */

			var core2 = CoreConfig.Load(w => Console.WriteLine(w.Message), licenseFileLocation: @"c:\temp\");

			/* Alternately, when using the CoreConfig.Load() approach, the license file location may be configured in the appSettings (or user secrets) file itself,
			 * parallel to the CHSAuthKey property.  Note that path separators (\ in Windows, / in Linux) must be escaped with a \ character, e.g.:
			 * 
			 	{
					"CHSAuthKey": "0D026F73-2BF5-4F61-88B0-59A223BB3C82",
					"LicenseFileLocation": "c:\\temp",
					"Archer": {
						"url": "http://archer.local/",
						"instance": "MyArcherInstance",
						.. etc...
					}
				}

			/* Note that the license filename cannot be overridden, only the directory location where it is stored.  Also, the trailing slash is optional in all cases.
			 * If the specified directory does not exist, Estrelica.Core will attempt to create it, raising an exception if this fails. */
		}

		static void CustomLicensePersistence()
		{
			/* As mentioned above, Estrelica.Core expects to be able to persist its license information in a simple text file on the local filesystem.  This may
			 * not be possible in some scenarios, such as when implementing an AWS Lambda or Azure Function where no local file access is available.
			 * The license information is a simple text string, so the specifics of how it is persisted are not significant, and you may provide your own
			 * methods to load and save this text between invocations of your application: */

			Func<string> myCustomLoadLicenseMethod = () =>
			{
				string licenseText = "xxx"; // <- here you must actually load the license from wherever you have stored it previously, e.g. from an S3 bucket, from a SQL server, etc.
				return licenseText;
			};

			Func<string, bool> myCustomSaveLicenseMethod = (licenseText) =>
			{
				// Here you must save whatever is passed in the licenseText parameter to your preferred persistence mechanism, e.g. an S3 bucket, a record in a SQL table, etc.
				return true; // and return true or false to indicate success
			};

			/* These functions may then be passed via the loadLicense and saveLicense parameters available on both the Estrelica.Core.ValidateLicense() method (if 
			 * you're handling your own authentication): */

			var core1 = Estrelica.Core.ValidateLicense(new Guid("xxx"), w => Console.WriteLine(w.Message), 
				loadLicense: myCustomLoadLicenseMethod, saveLicense: myCustomSaveLicenseMethod);

			/* and on the CoreConfig.Load method (if you're using a standard appSettings file): */

			var core2 = CoreConfig.Load(w => Console.WriteLine(w.Message), loadLicense: myCustomLoadLicenseMethod, saveLicense: myCustomSaveLicenseMethod);

			/* Note that if you override the persistence you must implement both the load and the save methods, otherwise Estrelica.Core will throw an ArgumentException. */

		}

		static void HeaderInjectionAndRequestExceptionHandling()
		{
			/* In some scenarios it may be necessary to add additional request headers to the HTTP requests made by Estrelica.Core.
			 * You can do this by assigning an Action<IDictionary<string,string>> to Estrelica.Core.RequestHeaderCallback.
			 * If this is set, Estrelica.Core will call this Action prior to each HTTP request that it makes, allowing you to
			 * add whatever request header keys/values you would like.  For example, here's how it might be used to inject a 
			 * custom SSO token into each HTTP request: */

			Func<string> getNewSSOToken = () =>
			{
				// Do something here to authenticate with the SSO provider and get a fresh token...
				return "xxxx"; // <- the actual token would be returned here, of course.
			};

			string ssoToken = getNewSSOToken();

			Estrelica.Core.RequestHeaderCallback = (requestHeaders) =>
			{
				requestHeaders["my-custom-SSO-token"] = ssoToken;
			};

			/* You can also assign a Func<Exception,bool> method to Estrelica.Core.ExceptionRetryCallback, which will be 
			 * called each time an exception occurs during an HTTP request that Estrelica.Core doesn't know how to deal with.
			 * If you return 'true' from this function, Estrelica.Core will re-attempt the failed HTTP call (up to 5 times
			 * before aborting the attempt and surfacing the exception).
			 * 
			 * Combined with the above, you might use this to detect when your SSO token has become invalid, refresh the SSO
			 * token, then retry the HTTP request: */

			Estrelica.Core.ExceptionRetryCallback = (ex) =>
			{
				if (ex is HttpRequestException && ex.Message.Contains("SSO token invalid"))
				{
					// This is the exception we expect when the SSO token has expired, so we'll refresh it and
					// allow Estrelica.Core to try again:
					ssoToken = getNewSSOToken(); // refresh the token
					return true; // Tell Estrelica.Core that we've handled the error and we want the API call to be tried again.
				}
				else
				{
					// This is some other exception that we didn't anticipate, so let Estrelica.Core's normal handling
					// of the error take over:
					return false;
				}
			};
		}

	}
}
