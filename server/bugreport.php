<?
//////////////////////////Main Configuration ///////////////////////////
//Open mysql connection
include_once('opendb.php');
include_once('xml_helper.php');

$sendmail = false;
$to = "...";
$from = "bugreport@yourdomain.com";

//////////////////////////End Main Configuration ///////////////////////////

$appname=$_POST["appname"];
$platform=$_POST["platform"];
$version=$_POST["version"];
$text=$_POST["text"];

$xml = "";
xml_create($xml);
xml_addStartTag($xml, "root");

$stmt = $db->prepare("Select id ".
					 "from updater_applications ".
					 "where name = ? ".
					 "  and version = ? ".
					 "  and platform = ?");
$stmt->bind_param('sss', $appname, $version, $platform);
$stmt->execute();
$stmt->store_result();

if ($stmt->num_rows == 0){
    //Application not found:
    xml_addElement($xml, "ERRORCODE", "ERR001");
}else{
    //Application found:
	$id = 0;
	$stmt->bind_result($id);
	$stmt->fetch();

	$stmt = $db->prepare("insert into updater_bugs(app_id, date, text) ".
						 "values(?, NOW(), ?)");
	$stmt->bind_param('is', $id, $text);
	$stmt->execute();

    xml_addStartTag($xml, "app");
    xml_addAttribute($xml, "id", $id);
    xml_addEndTag($xml, "app");

	if ($sendmail){
		$subject = "BUG-REPORT: ".$appname." v.".$version;
		$headers = "From: ".$from;
		$body = "App name: ".$appname."\n".
				"Platform: ".$platform."\n".
				"Version : ".$version."\n\n".
				$text;
		mail($to, $subject, $body, $headers);
	}
}

xml_addEndTag($xml, "root");
echo $xml;

$stmt->close();

//Close mysql connection
$db->close();
?>
