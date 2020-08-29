#import <Foundation/Foundation.h>
#import "Bugsee.h"

static BugseeAttachmentsDecisionBlock bgs_attachmentsDecisionBlock;

@interface BugseePlugin : NSObject <BugseeDelegate>

+ (instancetype)shared;

@end

id deserialize(const char* string)
{
    if (string) {
        NSError *err = nil;
        NSData *jsonData = [[NSString stringWithUTF8String:string] dataUsingEncoding:NSUTF8StringEncoding];
        NSDictionary *dict = [NSJSONSerialization JSONObjectWithData:jsonData
                                                             options:NSJSONReadingAllowFragments
                                                               error:&err];
        if (!err && dict) {
            return dict;
        } else {
            return nil;
        }
    } else {
        return nil;
    }
}

const char * serialize(id obj, size_t * size)
{
    if ([obj isKindOfClass:[NSString class]]) {
        NSString * str = [NSString stringWithFormat:@"\"%@\"",obj];
        *size = str.length;
        return str.UTF8String;
    } else if ([obj isKindOfClass:[NSNumber class]]){
        NSString * str = [NSString stringWithFormat:@"%@",obj];
        *size = str.length;
        return str.UTF8String;
    }else{
        NSData * data = [NSJSONSerialization dataWithJSONObject:obj options:0 error:nil];
        NSString * str = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
        *size = str.length;
        return str.UTF8String;
    }
}

void _bugsee_launch(char *appToken, char* options)
{
    NSMutableDictionary* dict;
    if (options)
        dict = [NSMutableDictionary dictionaryWithDictionary:deserialize(options)];

    Bugsee * bgs =[Bugsee launchWithToken:[NSString stringWithUTF8String:appToken]
                               andOptions:dict];

    bgs.delegate = [BugseePlugin shared];
}

void _bugsee_relaunch(char* options)
{
    NSMutableDictionary* dict;
    if (options)
        dict = [NSMutableDictionary dictionaryWithDictionary:deserialize(options)];

    [Bugsee relaunchWithDictionaryOptions:dict];
}

void _bugsee_stop()
{
    [Bugsee stop:nil];
}

void _bugsee_show_report(char *summary, char *description, int severity)
{
    if (summary) {
        [Bugsee showReportControllerWithSummary:[NSString stringWithUTF8String:summary]
                                    description:[NSString stringWithUTF8String:description]
                                       severity:severity];
    } else {
        [Bugsee showReportController];
    }
}

void _bugsee_pause()
{
    [Bugsee pause];
}

void _bugsee_resume()
{
    [Bugsee resume];
}

void _bugsee_traceKey(char *name, char* wrapperDict)
{
    NSDictionary* dict = deserialize(wrapperDict);
    [Bugsee traceKey:[NSString stringWithUTF8String:name]
           withValue:dict];
}

void _bugsee_registerEvent(char *name, char* params)
{
    if (params)
        [Bugsee registerEvent:[NSString stringWithUTF8String:name]
                   withParams:deserialize(params)];
}

void _bugsee_upload(char *summary, char *description, int severity)
{
    [Bugsee uploadWithSummary:[NSString stringWithUTF8String:summary]
                  description:[NSString stringWithUTF8String:description]
                     severity:severity];

}

void _bugsee_logException(char *name, char *reason, char *frames, bool handled)
{
    [Bugsee logException:[NSString stringWithUTF8String:name]
                  reason:[NSString stringWithUTF8String:reason]
                  frames:deserialize(frames)
                    type:@"unity"
                 handled:(handled?@(YES):@(NO))];
}

void _bugsee_log(char *message, int level)
{
    BugseeLogLevel lvl = (BugseeLogLevel)level;
    [Bugsee log:[NSString stringWithUTF8String:message] level:lvl];
}

void _bugsee_feedback()
{
    [Bugsee showFeedbackController];
}

void _bugsee_set_def_feedback_greeting(char *message)
{
    [Bugsee setDefaultFeedbackGreeting:[NSString stringWithUTF8String:message]];
}

void _bugsee_assert(bool condition, char *description)
{
    BUGSEE_ASSERT(condition, [NSString stringWithUTF8String:description]);
}

bool _bugsee_set_attribute(char * value)
{
    NSDictionary* dict = deserialize(value);
    if (!dict) return false;
    return [Bugsee setAttribute:dict[@"key"]
                      withValue:dict[@"value"]];
}

const char * _bugsee_get_attribute(char * key)
{
    id attr = [Bugsee getAttribute:[NSString stringWithUTF8String:key]];
    if (!attr) return NULL;
    size_t size;
    const char * result = serialize(attr, &size);
    char * copyResult = malloc(size);
    memcpy((void*)copyResult, result, size);

    return copyResult;
}


void _bugsee_set_email(const char * email){
    [Bugsee setEmail:[NSString stringWithUTF8String:email]];
}

const char * _bugsee_get_device_id()
{
    NSString * deviceId = [Bugsee getDeviceId];
    if (!deviceId) return NULL;

    size_t size = deviceId.length;
    char * copyResult = malloc(size);
    memcpy((void*)copyResult, deviceId.UTF8String, size);

    return copyResult;
}

const char * _bugsee_get_email()
{
    NSString * email = [Bugsee getEmail];

    if (!email || email.length < 1) return NULL;

    size_t size = email.length;
    char * copyResult = malloc(size);
    memcpy((void*)copyResult, email.UTF8String, size);

    return copyResult;
}

void _bugsee_clear_email()
{
    [Bugsee clearEmail];
}

bool _bugsee_clear_attribute(char * key)
{
    return [Bugsee clearAttribute:[NSString stringWithUTF8String:key]];
}

void _bugsee_setAttachmentsForReport(char * attachments)
{
    if (attachments == NULL){
        bgs_attachmentsDecisionBlock(nil);
        return;
    }

    NSMutableArray<BugseeAttachment *> * result = [NSMutableArray arrayWithCapacity:3];
    NSArray * attachmentsRaw = deserialize(attachments);

    for (NSDictionary * attachmentDict in attachmentsRaw) {
        BugseeAttachment * attachment = [BugseeAttachment new];
        attachment.name = attachmentDict[@"name"];
        attachment.filename = attachmentDict[@"filename"];
        NSString * path = attachmentDict[@"path"];
        attachment.data = [NSData dataWithContentsOfFile:path];
        [[NSFileManager defaultManager] removeItemAtPath:path error:nil];

        [result addObject:attachment];
    }

    bgs_attachmentsDecisionBlock(result);
}

void _bugsee_testExceptionCrash()
{
    [Bugsee testExceptionCrash];
}

void _bugsee_testSignalCrash()
{
    [Bugsee testSignalCrash];
}

bool _bugsee_addSecureRect(char * dictionary)
{
    NSDictionary * dict = deserialize(dictionary);
    CGRect rect;
    if (CGRectMakeWithDictionaryRepresentation((CFDictionaryRef)dict, &rect)){

        CGFloat scale = [UIScreen mainScreen].scale;
        rect.size.width /= scale; rect.size.height /= scale;
        rect.origin.x /= scale; rect.origin.y /= scale;

        return [Bugsee addSecureRect:rect];
    }

    return false;
}

bool _bugsee_removeSecureRect(char * dictionary)
{
    NSDictionary * dict = deserialize(dictionary);
    CGRect rect;
    if (CGRectMakeWithDictionaryRepresentation((CFDictionaryRef)dict, &rect)){
        CGFloat scale = [UIScreen mainScreen].scale;
        rect.size.width /= scale; rect.size.height /= scale;
        rect.origin.x /= scale; rect.origin.y /= scale;

        return [Bugsee removeSecureRect:rect];
    }

    return false;
}

void _bugsee_removeAllSecureRects()
{
    [Bugsee removeAllSecureRects];
}

const char * _bugsee_getAllSecureRects()
{
    NSArray * rects = [Bugsee getAllSecureRects];
    if (rects == nil || rects.count < 1) return NULL;

    NSMutableArray * rectsDicts = [NSMutableArray arrayWithCapacity:rects.count];
    for (NSValue * rectV in rects){
        CGFloat scale = [UIScreen mainScreen].scale;
        CGRect rect = [rectV CGRectValue];
        [rectsDicts addObject:@(rect.origin.x * scale)];
        [rectsDicts addObject:@(rect.origin.y * scale)];
        [rectsDicts addObject:@(rect.size.width * scale)];
        [rectsDicts addObject:@(rect.size.height * scale)];
    }

    size_t len = 0;
    const char * json = serialize(rectsDicts, &len);

    if (json == NULL || len < 1) return NULL;

    char * copyResult = malloc(len);
    memcpy((void*)copyResult, json, len);

    return copyResult;
}

@implementation BugseePlugin

+ (instancetype)shared {
    static BugseePlugin *_shared = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        _shared = [BugseePlugin new];
    });

    return _shared;
}

-(void)bugseeAttachmentsForReport:(BugseeReport *)report completionHandler:(nonnull BugseeAttachmentsDecisionBlock)decisionBlock
{
    NSString * params = [NSString stringWithFormat:@"%@,%lu", report.type, (unsigned long)report.severity];
    UnitySendMessage("bgs_gameObject", "AttachmentsForReport", params.UTF8String);
    bgs_attachmentsDecisionBlock = decisionBlock;
}

-(void)bugseeLifecycleEvent:(BugseeLifecycleEventType)eventType
{
    NSString *tmp = [NSString stringWithFormat:@"%i", (int)eventType];
    UnitySendMessage("bgs_gameObject", "LifecycleEvent", tmp.UTF8String);
}

@end

